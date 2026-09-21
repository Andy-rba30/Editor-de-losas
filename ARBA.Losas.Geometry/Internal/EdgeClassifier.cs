using System;
using System.Collections.Generic;

namespace ARBA.Losas.Geometry.Internal
{
    /// <summary>Decide sobre que curva original descansa una arista entre dos vertices canonicos.</summary>
    internal static class EdgeClassifier
    {
        /// <summary>
        /// Id de la curva original comun a ambos vertices cuyo punto medio queda a menos de la
        /// tolerancia, o -1 si la arista es un tramo recto nuevo (corte). Si hay varias
        /// candidatas (por ejemplo dos arcos entre los mismos extremos) gana la mas cercana.
        /// </summary>
        public static int Classify(RegistryPoint a, RegistryPoint b, CurveTable table, double tolerance)
        {
            Vec2 mid = (a.Position + b.Position) * 0.5;
            int best = -1;
            double bestDist = double.MaxValue;
            foreach (Membership ma in a.Memberships)
            {
                if (!b.TryGetT(ma.CurveId, out _)) continue;
                ICurve2 c = table[ma.CurveId];
                double d = c.PointAt(c.ClosestParameter(mid)).DistanceTo(mid);
                if (d <= tolerance && d < bestDist) { bestDist = d; best = ma.CurveId; }
            }
            return best;
        }

        /// <summary>Tolerancia de clasificacion: la flecha maxima del teselado mas el redondeo de Clipper.</summary>
        public static double Tolerance(double tessellationTolerance, double scale) =>
            tessellationTolerance * 1.5 + 4.0 / scale;
    }
}
