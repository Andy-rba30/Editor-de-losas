namespace ARBA.Losas.Geometry
{
    /// <summary>Opciones de la union. Todas las distancias en unidades internas (pies).</summary>
    public sealed class MergeOptions
    {
        /// <summary>Microgaps de hasta esta distancia entre losas se cierran. Por defecto 1 mm.</summary>
        public double JoinTolerance { get; init; } = Tolerances.OneMillimeter;
        public double TessellationTolerance { get; init; } = Tolerances.OneMillimeter;
        public double ShortCurveTolerance { get; init; } = Tolerances.DefaultShortCurve;
        public double MinArea { get; init; } = 0.1;
        public double Scale { get; init; } = 1e6;
    }
}
