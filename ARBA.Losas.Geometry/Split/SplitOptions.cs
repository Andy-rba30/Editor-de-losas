namespace ARBA.Losas.Geometry
{
    /// <summary>Opciones de la division. Todas las distancias en unidades internas (pies).</summary>
    public sealed class SplitOptions
    {
        /// <summary>Desviacion maxima al teselar arcos. Por defecto 1 mm.</summary>
        public double TessellationTolerance { get; init; } = Tolerances.OneMillimeter;

        /// <summary>
        /// Tolerancia de curva corta: ningun tramo del resultado sera menor. El proyecto
        /// Revit la lee de Application.ShortCurveTolerance y la pasa aqui.
        /// </summary>
        public double ShortCurveTolerance { get; init; } = Tolerances.DefaultShortCurve;

        /// <summary>Area minima de una region resultante; las menores se descartan como slivers.</summary>
        public double MinArea { get; init; } = 0.1;

        /// <summary>Factor de escala a enteros de Clipper2. Se guarda para deshacer la conversion.</summary>
        public double Scale { get; init; } = 1e6;

        /// <summary>Si es true cada linea se extiende hasta cubrir toda la losa; si no, se usa tal cual.</summary>
        public bool ExtendCutLines { get; init; } = true;
    }
}
