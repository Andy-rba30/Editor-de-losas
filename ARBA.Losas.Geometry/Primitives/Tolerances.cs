namespace ARBA.Losas.Geometry
{
    /// <summary>Tolerancias por defecto en unidades internas de Revit (pies).</summary>
    public static class Tolerances
    {
        /// <summary>1 mm en pies: tolerancia de teselado de arcos por defecto.</summary>
        public const double OneMillimeter = 1.0 / 304.8;

        /// <summary>
        /// Tolerancia de curva corta de Revit (Application.ShortCurveTolerance, ~0.8 mm).
        /// Es solo el valor por defecto de las opciones: el proyecto Revit pasa el real.
        /// </summary>
        public const double DefaultShortCurve = 0.00256026455729167;

        /// <summary>Distancia por debajo de la cual dos puntos se consideran el mismo al cerrar contornos.</summary>
        public const double Closure = 1e-6;

        /// <summary>Tolerancia angular (seno del angulo) para considerar dos direcciones colineales.</summary>
        public const double Collinear = 1e-9;
    }
}
