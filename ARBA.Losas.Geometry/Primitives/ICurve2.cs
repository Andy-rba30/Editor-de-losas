using System.Collections.Generic;

namespace ARBA.Losas.Geometry
{
    /// <summary>Punto de una curva junto con su parametro normalizado (0..1).</summary>
    public readonly struct CurvePoint
    {
        public Vec2 Point { get; }
        public double T { get; }
        public CurvePoint(Vec2 point, double t) { Point = point; T = t; }
    }

    /// <summary>
    /// Curva plana acotada, parametrizada de 0 (inicio) a 1 (fin). Equivale a la
    /// Curve de Revit en la frontera, pero sin depender de ella.
    /// </summary>
    public interface ICurve2
    {
        Vec2 Start { get; }
        Vec2 End { get; }
        double Length { get; }

        /// <summary>Curva de entrada de la que procede este tramo, o null si es un tramo nuevo (corte).</summary>
        CurveOrigin Origin { get; }

        /// <summary>Punto en el parametro normalizado <paramref name="t"/> (se admite fuera de 0..1 para lineas).</summary>
        Vec2 PointAt(double t);

        /// <summary>Parametro normalizado (0..1) del punto de la curva mas cercano a <paramref name="p"/>.</summary>
        double ClosestParameter(Vec2 p);

        /// <summary>
        /// Recorte entre dos parametros normalizados. Si <paramref name="t0"/> es mayor que
        /// <paramref name="t1"/> la curva resultante queda invertida (equivale a MakeBound).
        /// </summary>
        ICurve2 Trim(double t0, double t1);

        ICurve2 Reversed();

        /// <summary>
        /// Polilinea que aproxima la curva con desviacion maxima <paramref name="tolerance"/>.
        /// Incluye ambos extremos y el parametro de cada vertice.
        /// </summary>
        IReadOnlyList<CurvePoint> Tessellate(double tolerance);
    }
}
