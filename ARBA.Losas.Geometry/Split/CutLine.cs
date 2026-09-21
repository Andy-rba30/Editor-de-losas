using System;

namespace ARBA.Losas.Geometry
{
    /// <summary>Linea de corte proyectada al plano de la losa, definida por dos puntos.</summary>
    public sealed class CutLine
    {
        public Vec2 A { get; }
        public Vec2 B { get; }

        public CutLine(Vec2 a, Vec2 b)
        {
            if (a.DistanceTo(b) <= 0) throw new ArgumentException("La linea de corte necesita dos puntos distintos.");
            A = a;
            B = b;
        }

        public Vec2 Direction => (B - A).Normalized();
        public double Length => A.DistanceTo(B);

        /// <summary>Parametro (distancia con signo desde A a lo largo de la direccion) de la proyeccion de un punto.</summary>
        public double ProjectParameter(Vec2 p) => (p - A).Dot(Direction);

        public Vec2 PointAtParameter(double s) => A + Direction * s;

        /// <summary>Distancia con signo del punto a la recta: positiva a la izquierda de A->B.</summary>
        public double SignedDistance(Vec2 p) => Direction.Cross(p - A);

        public override string ToString() => "Cut " + A + " -> " + B;
    }
}
