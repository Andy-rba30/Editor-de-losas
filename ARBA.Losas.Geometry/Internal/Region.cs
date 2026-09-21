using System.Collections.Generic;

namespace ARBA.Losas.Geometry.Internal
{
    /// <summary>Anillo cerrado de vertices canonicos (el ultimo enlaza con el primero).</summary>
    internal sealed class Ring : List<RegistryPoint>
    {
        public Ring() { }
        public Ring(IEnumerable<RegistryPoint> points) : base(points) { }
    }

    /// <summary>Region de trabajo: anillo exterior y huecos, todos como vertices canonicos.</summary>
    internal sealed class Region
    {
        public Ring Outer { get; }
        public List<Ring> Holes { get; }

        public Region(Ring outer, List<Ring> holes)
        {
            Outer = outer;
            Holes = holes ?? new List<Ring>();
        }

        public IEnumerable<Ring> AllRings()
        {
            yield return Outer;
            foreach (Ring h in Holes) yield return h;
        }

        /// <summary>Aristas (a, b) de todos los anillos, con los vertices ya resueltos y sin duplicados consecutivos.</summary>
        public List<(RegistryPoint A, RegistryPoint B)> Edges(PointRegistry reg)
        {
            var edges = new List<(RegistryPoint, RegistryPoint)>();
            foreach (Ring ring in AllRings())
            {
                List<RegistryPoint> pts = ResolvedDistinct(ring, reg);
                for (int i = 0; i < pts.Count; i++)
                {
                    RegistryPoint a = pts[i], b = pts[(i + 1) % pts.Count];
                    if (!ReferenceEquals(a, b)) edges.Add((a, b));
                }
            }
            return edges;
        }

        /// <summary>Vertices del anillo resueltos a su canonico, sin repeticiones consecutivas ni cierre duplicado.</summary>
        public static List<RegistryPoint> ResolvedDistinct(Ring ring, PointRegistry reg)
        {
            var pts = new List<RegistryPoint>(ring.Count);
            foreach (RegistryPoint p in ring)
            {
                RegistryPoint r = reg.Resolve(p);
                if (pts.Count > 0 && ReferenceEquals(pts[pts.Count - 1], r)) continue;
                pts.Add(r);
            }
            while (pts.Count > 1 && ReferenceEquals(pts[0], pts[pts.Count - 1])) pts.RemoveAt(pts.Count - 1);
            return pts;
        }
    }
}
