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
            int curveIndex = 0;
            Ring outer = TessellateContour(polygon.Outer, table, reg, tolerance, slab, ref curveIndex);
            var holes = new List<Ring>();
            foreach (Contour h in polygon.Holes) holes.Add(TessellateContour(h, table, reg, tolerance, slab, ref curveIndex));
            return new Region(outer, holes);
        }

        private static Ring TessellateContour(Contour contour, CurveTable table, PointRegistry reg, double tolerance, int slab, ref int curveIndex)
        {
            var ring = new Ring();
            int firstId = -1, prevId = -1;
            RegistryPoint firstVertex = null;
            foreach ((ICurve2 curve, CurveOrigin origin) in SplitLargeArcs(contour.Curves, slab, ref curveIndex))
            {
                int id = table.Add(curve, origin);
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
        private static List<(ICurve2, CurveOrigin)> SplitLargeArcs(IReadOnlyList<ICurve2> curves, int slab, ref int curveIndex)
        {
            var result = new List<(ICurve2, CurveOrigin)>();
            foreach (ICurve2 c in curves)
            {
                int index = curveIndex++;
                if (c is Arc2 arc && System.Math.Abs(arc.SweepAngle) > System.Math.PI + 1e-12)
                {
                    result.Add((arc.Trim(0, 0.5), new CurveOrigin(slab, index, 0, 0.5)));
                    result.Add((arc.Trim(0.5, 1), new CurveOrigin(slab, index, 0.5, 1)));
                }
                else result.Add((c, new CurveOrigin(slab, index, 0, 1)));
            }
            return result;
        }
    }
}
