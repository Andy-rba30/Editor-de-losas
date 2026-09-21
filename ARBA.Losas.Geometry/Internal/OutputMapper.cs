using System.Collections.Generic;
using Clipper2Lib;

namespace ARBA.Losas.Geometry.Internal
{
    /// <summary>
    /// Traduce la salida de Clipper2 a regiones de vertices canonicos. Los vertices que ya
    /// existian se reconocen por proximidad; los nuevos (intersecciones con el corte) se
    /// localizan sobre la arista de entrada que los contiene, se proyectan sobre la curva
    /// original verdadera y quedan registrados con su parametro.
    /// </summary>
    internal sealed class OutputMapper
    {
        private readonly PointRegistry _reg;
        private readonly CurveTable _table;
        private readonly double _classifyTolerance;
        private readonly List<GeometryWarning> _warnings;

        public OutputMapper(PointRegistry reg, CurveTable table, double classifyTolerance, List<GeometryWarning> warnings)
        {
            _reg = reg;
            _table = table;
            _classifyTolerance = classifyTolerance;
            _warnings = warnings;
        }

        /// <summary>
        /// Una salida de Clipper puede ser un poligono que se toca a si mismo en un vertice
        /// (dos piezas unidas solo por una esquina): se separa en tantas regiones como lazos.
        /// </summary>
        public List<Region> Map(Path64 outer, List<Path64> holes, List<(RegistryPoint A, RegistryPoint B)> inputEdges)
        {
            List<Ring> outers = SplitPinched(MapRing(outer, inputEdges));
            var holeRings = new List<Ring>();
            foreach (Path64 h in holes) holeRings.AddRange(SplitPinched(MapRing(h, inputEdges)));

            var regions = new List<Region>();
            if (outers.Count == 1)
            {
                regions.Add(new Region(outers[0], holeRings));
                return regions;
            }
            // varios exteriores: cada hueco va con el exterior que lo contiene
            var outerPaths = new List<Path64>();
            foreach (Ring o in outers) outerPaths.Add(ClipperBridge.ToPath(o, _reg));
            var assigned = new List<List<Ring>>();
            foreach (Ring _ in outers) assigned.Add(new List<Ring>());
            foreach (Ring h in holeRings)
            {
                List<RegistryPoint> pts = Region.ResolvedDistinct(h, _reg);
                if (pts.Count == 0) continue;
                for (int i = 0; i < outers.Count; i++)
                    if (Clipper.PointInPolygon(pts[0].Key, outerPaths[i]) != PointInPolygonResult.IsOutside) { assigned[i].Add(h); break; }
            }
            for (int i = 0; i < outers.Count; i++) regions.Add(new Region(outers[i], assigned[i]));
            return regions;
        }

        /// <summary>
        /// Clipper puede omitir el vertice de contacto en uno de los dos pasos (queda colineal
        /// en la arista que lo atraviesa): si un vertice cae sobre una arista no adyacente del
        /// mismo anillo, se inserta en ella para que el lazo se detecte como vertice repetido.
        /// </summary>
        private static List<RegistryPoint> InsertTouchingVertices(List<RegistryPoint> pts)
        {
            int n = pts.Count;
            if (n < 4) return pts;
            var insertions = new List<(int After, double T, RegistryPoint Point)>();
            for (int i = 0; i < n; i++)
            {
                RegistryPoint a = pts[i], b = pts[(i + 1) % n];
                for (int k = 0; k < n; k++)
                {
                    if (k == i || k == (i + 1) % n) continue;
                    RegistryPoint v = pts[k];
                    if (ReferenceEquals(v, a) || ReferenceEquals(v, b)) continue;
                    double d = ClipperBridge.DistanceToSegment(v.Key, a.Key, b.Key, out double t);
                    if (d <= PointRegistry.MatchTolerance && t > 0 && t < 1) insertions.Add((i, t, v));
                }
            }
            if (insertions.Count == 0) return pts;
            insertions.Sort((x, y) => x.After != y.After ? x.After.CompareTo(y.After) : x.T.CompareTo(y.T));
            var result = new List<RegistryPoint>(n + insertions.Count);
            int c = 0;
            for (int i = 0; i < n; i++)
            {
                result.Add(pts[i]);
                while (c < insertions.Count && insertions[c].After == i) result.Add(insertions[c++].Point);
            }
            return result;
        }

        /// <summary>Separa un anillo en lazos independientes por cada vertice repetido.</summary>
        internal List<Ring> SplitPinched(Ring ring)
        {
            var result = new List<Ring>();
            var pending = new Stack<List<RegistryPoint>>();
            pending.Push(InsertTouchingVertices(Region.ResolvedDistinct(ring, _reg)));
            while (pending.Count > 0)
            {
                List<RegistryPoint> pts = pending.Pop();
                if (pts.Count < 3) continue;
                var seen = new Dictionary<RegistryPoint, int>();
                int i = -1, j = -1;
                for (int k = 0; k < pts.Count; k++)
                {
                    if (seen.TryGetValue(pts[k], out int first)) { i = first; j = k; break; }
                    seen[pts[k]] = k;
                }
                if (i < 0) { result.Add(new Ring(pts)); continue; }
                // el lazo interior pts[i..j-1] se separa del resto
                List<RegistryPoint> inner = pts.GetRange(i, j - i);
                var rest = new List<RegistryPoint>(pts.Count - (j - i));
                rest.AddRange(pts.GetRange(0, i));
                rest.AddRange(pts.GetRange(j, pts.Count - j));
                pending.Push(inner);
                pending.Push(rest);
            }
            return result;
        }

        private Ring MapRing(Path64 path, List<(RegistryPoint A, RegistryPoint B)> inputEdges)
        {
            var ring = new Ring();
            foreach (Point64 pt in path)
            {
                RegistryPoint p = _reg.Find(pt) ?? Classify(pt, inputEdges);
                ring.Add(p);
            }
            return ring;
        }

        private RegistryPoint Classify(Point64 pt, List<(RegistryPoint A, RegistryPoint B)> inputEdges)
        {
            Vec2 raw = _reg.FromKey(pt);
            Vec2 position = raw;
            var memberships = new List<Membership>();
            bool snapped = false;
            foreach ((RegistryPoint a, RegistryPoint b) in inputEdges)
            {
                double d = ClipperBridge.DistanceToSegment(pt, a.Key, b.Key, out _);
                if (d > PointRegistry.MatchTolerance) continue;
                int curveId = EdgeClassifier.Classify(a, b, _table, _classifyTolerance);
                if (curveId < 0) continue;
                ICurve2 curve = _table[curveId];
                double t = curve.ClosestParameter(raw);
                memberships.Add(new Membership(curveId, t));
                if (!snapped)
                {
                    // el punto de corte se lleva a la curva verdadera, no a la polilinea
                    position = curve.PointAt(t);
                    snapped = true;
                }
            }
            RegistryPoint p = _reg.GetOrAdd(position, snapped ? VertexKind.OnCurve : VertexKind.Cut, out bool created);
            foreach (Membership m in memberships) p.AddMembership(m.CurveId, m.T);
            // la coordenada sin proyectar tambien debe resolver a este punto (la vera la region vecina)
            if (created || !p.Key.Equals(pt)) _reg.AddAlias(pt, p);
            return p;
        }
    }
}
