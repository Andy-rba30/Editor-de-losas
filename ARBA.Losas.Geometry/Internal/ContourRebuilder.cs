using System;
using System.Collections.Generic;

namespace ARBA.Losas.Geometry.Internal
{
    /// <summary>
    /// Reconstruye contornos con curvas verdaderas a partir de anillos de vertices canonicos:
    /// las cadenas de vertices sobre la misma curva original se sustituyen por esa curva
    /// recortada; el resto son tramos rectos (cortes).
    /// </summary>
    internal sealed class ContourRebuilder
    {
        private readonly CurveTable _table;
        private readonly PointRegistry _reg;
        private readonly double _classifyTolerance;

        public ContourRebuilder(CurveTable table, PointRegistry reg, double classifyTolerance)
        {
            _table = table;
            _reg = reg;
            _classifyTolerance = classifyTolerance;
        }

        /// <summary>Poligono reconstruido, o null si la region quedo degenerada.</summary>
        public Polygon2 Rebuild(Region region)
        {
            Contour outer = RebuildRing(region.Outer);
            if (outer == null) return null;
            var holes = new List<Contour>();
            foreach (Ring h in region.Holes)
            {
                Contour c = RebuildRing(h);
                if (c != null && c.Area > 0) holes.Add(c);
            }
            return new Polygon2(outer, holes);
        }

        public Contour RebuildRing(Ring ring)
        {
            List<RegistryPoint> pts = Region.ResolvedDistinct(ring, _reg);
            int n = pts.Count;
            if (n < 3) return null;

            var edgeCurve = new int[n];
            for (int i = 0; i < n; i++)
                edgeCurve[i] = EdgeClassifier.Classify(pts[i], pts[(i + 1) % n], _table, _classifyTolerance);

            // caso especial: todo el anillo es una unica curva cerrada (circunferencia completa)
            bool allSame = true;
            for (int i = 1; i < n; i++) if (edgeCurve[i] != edgeCurve[0]) { allSame = false; break; }
            if (allSame && edgeCurve[0] >= 0)
            {
                pts[1].TryGetT(edgeCurve[0], out double t1);
                bool forward = t1 < 0.5;
                return new Contour(new[] { BuildOnCurve(edgeCurve[0], pts[0], pts[0], forward ? 0 : 1, forward ? 1 : 0) });
            }

            // rotar para que el anillo empiece donde cambia la curva
            int start = 0;
            for (int i = 0; i < n; i++)
                if (edgeCurve[i] != edgeCurve[(i - 1 + n) % n]) { start = i; break; }

            var curves = new List<ICurve2>();
            int idx = 0;
            while (idx < n)
            {
                int e = (start + idx) % n;
                int c = edgeCurve[e];
                int len = 1;
                if (c >= 0)
                    while (idx + len < n && edgeCurve[(start + idx + len) % n] == c) len++;
                RegistryPoint from = pts[e];
                RegistryPoint to = pts[(e + len) % n];
                if (c < 0)
                {
                    for (int k = 0; k < len; k++)
                    {
                        RegistryPoint a = pts[(e + k) % n], b = pts[(e + k + 1) % n];
                        if (a.Position != b.Position) curves.Add(new Segment2(a.Position, b.Position));
                    }
                }
                else
                {
                    ICurve2 built = BuildOnCurve(c, from, to);
                    if (built != null) curves.Add(built);
                }
                idx += len;
            }
            curves = MergeCollinear(curves);
            if (curves.Count < 2) return null;
            try { return new Contour(curves); }
            catch (ArgumentException) { return null; }
        }

        /// <summary>Curva original <paramref name="curveId"/> recortada entre dos vertices canonicos, con los extremos exactamente en ellos.</summary>
        private ICurve2 BuildOnCurve(int curveId, RegistryPoint from, RegistryPoint to)
        {
            from.TryGetT(curveId, out double t0);
            to.TryGetT(curveId, out double t1);
            return BuildOnCurve(curveId, from, to, t0, t1);
        }

        private ICurve2 BuildOnCurve(int curveId, RegistryPoint from, RegistryPoint to, double t0, double t1)
        {
            Vec2 p0 = from.Position, p1 = to.Position;
            ICurve2 curve = _table[curveId];
            bool whole = ReferenceEquals(from, to);
            if (p0 == p1 && !whole) return null;
            // procedencia en parametros de la curva de entrada (la tabla puede contener mitades de un arco grande)
            CurveOrigin tableOrigin = _table.OriginOf(curveId);
            var origin = new CurveOrigin(tableOrigin.Slab, tableOrigin.Curve,
                tableOrigin.T0 + t0 * (tableOrigin.T1 - tableOrigin.T0),
                tableOrigin.T0 + t1 * (tableOrigin.T1 - tableOrigin.T0));
            if (curve is Segment2) return new Segment2(p0, p1, origin);
            if (t0 == t1 || curve is not Arc2 arc) return new Segment2(p0, p1, origin);
            Arc2 trimmed = (Arc2)arc.Trim(t0, t1);
            if (whole || (trimmed.Start.IsAlmostEqual(p0, 1e-9) && trimmed.End.IsAlmostEqual(p1, 1e-9)))
                return new Arc2(trimmed.Center, trimmed.Radius, trimmed.StartAngle, trimmed.SweepAngle, origin);
            // los extremos canonicos pueden diferir levemente del arco (fusiones): arco por tres puntos que pasa exactamente por ellos
            Vec2 mid = trimmed.PointAt(0.5);
            try { return Arc2.FromThreePoints(p0, mid, p1, origin); }
            catch (ArgumentException) { return new Segment2(p0, p1, origin); }
        }

        /// <summary>Une tramos rectos consecutivos colineales y con el mismo sentido (tambien entre el ultimo y el primero).</summary>
        internal static List<ICurve2> MergeCollinear(List<ICurve2> curves)
        {
            if (curves.Count < 2) return curves;
            var result = new List<ICurve2>(curves);
            bool changed = true;
            while (changed && result.Count >= 2)
            {
                changed = false;
                for (int i = 0; i < result.Count; i++)
                {
                    int j = (i + 1) % result.Count;
                    if (i == j) break;
                    if (result[i] is Segment2 s1 && result[j] is Segment2 s2 && AreCollinear(s1, s2))
                    {
                        CurveOrigin origin = null;
                        if (s1.Origin != null && s2.Origin != null && s1.Origin.Slab == s2.Origin.Slab && s1.Origin.Curve == s2.Origin.Curve)
                            origin = new CurveOrigin(s1.Origin.Slab, s1.Origin.Curve, s1.Origin.T0, s2.Origin.T1);
                        var merged = new Segment2(s1.Start, s2.End, origin);
                        if (j > i) { result[i] = merged; result.RemoveAt(j); }
                        else { result[j] = merged; result.RemoveAt(i); }
                        changed = true;
                        break;
                    }
                }
            }
            return result;
        }

        private static bool AreCollinear(Segment2 a, Segment2 b)
        {
            Vec2 d1 = a.End - a.Start, d2 = b.End - b.Start;
            double cross = Math.Abs(d1.Cross(d2));
            return cross <= Tolerances.Collinear * d1.Length * d2.Length && d1.Dot(d2) > 0;
        }
    }
}
