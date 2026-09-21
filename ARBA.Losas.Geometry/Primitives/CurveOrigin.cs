namespace ARBA.Losas.Geometry
{
    /// <summary>
    /// Procedencia de una curva reconstruida: de que curva de entrada viene y que tramo
    /// (parametros normalizados) ocupa sobre ella. Si T0 es mayor que T1 la curva recorre
    /// la original en sentido contrario. Las curvas nuevas (tramos de corte) no tienen origen.
    /// </summary>
    public sealed class CurveOrigin
    {
        /// <summary>Indice de la losa de entrada (0 en la division).</summary>
        public int Slab { get; }
        /// <summary>Indice de la curva dentro de la losa, contando el contorno exterior y luego los huecos, en orden.</summary>
        public int Curve { get; }
        public double T0 { get; }
        public double T1 { get; }

        public CurveOrigin(int slab, int curve, double t0, double t1)
        {
            Slab = slab; Curve = curve; T0 = t0; T1 = t1;
        }

        /// <summary>True si la curva de salida es la original completa y en su mismo sentido.</summary>
        public bool IsWholeForward => T0 == 0 && T1 == 1;

        public override string ToString() =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, "losa {0} curva {1} [{2:0.####}..{3:0.####}]", Slab, Curve, T0, T1);
    }
}
