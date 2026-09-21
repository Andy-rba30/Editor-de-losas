using System.Collections.Generic;

namespace ARBA.Losas.Geometry.Internal
{
    /// <summary>
    /// Convierte un Polygon2 en una Region de vertices canonicos: cada curva se tesela y
    /// cada vertice recuerda a que curva pertenece y con que parametro. Los vertices de
    /// union entre curvas pertenecen a las dos (fin de una, inicio de la siguiente).
    /// </summary>
    internal static class Tessellator
    {
        public static Region Tessellate(Polygon2 polygon, CurveTable table, PointRegistry reg, double tolerance, int slab)
        {
            Ring outer = TessellateContour(polygon.Outer, table, reg, tolerance, slab);
            var holes = new List<Ring>();
            foreach (Contour h in polygon.Holes) holes.Add(TessellateContour(h, table, reg, tolerance, slab));
            return new Region(outer, holes);
        }

        private static Ring TessellateContour(Contour contour, CurveTable table, PointRegistry reg, double tolerance, int slab)
        {
            var ring = new Ring();
            int firstId = -1, prevId = -1;
            RegistryPoint firstVertex = null;
            foreach (ICurve2 curve in SplitLargeArcs(contour.Curves))
            {
                int id = table.Add(curve);
                if (firstId < 0) firstId = id;
                IReadOnlyList<CurvePoint> pts = curve.Tessellate(tolerance);
                // el ultimo punto de cada curva es el primero de la siguiente: no se repite
                for (int k = 0; k < pts.Count - 1; k++)
                {
                    bool junction = k == 0;
                    RegistryPoint p = reg.GetOrAdd(pts[k].Point, junction ? VertexKind.Original : VertexKind.Tessellation, out _);
                    if (p.Slab < 0) p.Slab = slab;
                    p.AddMembership(id, pts[k].T);
                    if (junction)
                    {
                        if (prevId >= 0) p.AddMembership(prevId, 1);
                        else firstVertex = p;
                    }
                    if (ring.Count == 0 || !ReferenceEquals(ring[ring.Count - 1], p)) ring.Add(p);
                }
                prevId = id;
            }
            // el primer vertice tambien es el fin de la ultima curva
            if (firstVertex != null && prevId >= 0) firstVertex.AddMembership(prevId, 1);
            return ring;
        }

        /// <summary>
        /// Los arcos de mas de media vuelta se parten en dos: asi ninguna cadena de vertices
        /// sobre una misma curva puede "dar la vuelta" por su union (caso de la circunferencia
        /// completa) y el recorte por parametros queda sin ambiguedad.
        /// </summary>
        private static IEnumerable<ICurve2> SplitLargeArcs(IReadOnlyList<ICurve2> curves)
        {
            foreach (ICurve2 c in curves)
            {
                if (c is Arc2 arc && System.Math.Abs(arc.SweepAngle) > System.Math.PI + 1e-12)
                {
                    yield return arc.Trim(0, 0.5);
                    yield return arc.Trim(0.5, 1);
                }
                else yield return c;
            }
        }
    }
}
