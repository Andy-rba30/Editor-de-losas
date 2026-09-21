using System;
using System.Globalization;

namespace ARBA.Losas.Geometry
{
    /// <summary>
    /// Punto o vector en el plano de la losa. Las coordenadas van en unidades internas
    /// de Revit (pies), pero la libreria no depende de ello: solo las tolerancias lo asumen.
    /// </summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public double X { get; }
        public double Y { get; }

        public Vec2(double x, double y) { X = x; Y = y; }

        public static readonly Vec2 Zero = new Vec2(0, 0);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);
        public static Vec2 operator *(Vec2 a, double k) => new Vec2(a.X * k, a.Y * k);
        public static Vec2 operator *(double k, Vec2 a) => new Vec2(a.X * k, a.Y * k);
        public static Vec2 operator /(Vec2 a, double k) => new Vec2(a.X / k, a.Y / k);

        public double Dot(Vec2 o) => X * o.X + Y * o.Y;

        /// <summary>Producto vectorial 2D (componente Z): positivo si <paramref name="o"/> queda a la izquierda.</summary>
        public double Cross(Vec2 o) => X * o.Y - Y * o.X;

        public double Length => Math.Sqrt(X * X + Y * Y);
        public double LengthSquared => X * X + Y * Y;

        public double DistanceTo(Vec2 o) => (this - o).Length;

        /// <summary>Vector unitario. Lanza si el vector es nulo.</summary>
        public Vec2 Normalized()
        {
            double l = Length;
            if (l <= 0) throw new InvalidOperationException("No se puede normalizar un vector nulo.");
            return new Vec2(X / l, Y / l);
        }

        /// <summary>Vector girado 90 grados en sentido antihorario.</summary>
        public Vec2 Perpendicular() => new Vec2(-Y, X);

        public bool IsAlmostEqual(Vec2 o, double tolerance) => DistanceTo(o) <= tolerance;

        public bool Equals(Vec2 o) => X == o.X && Y == o.Y;
        public override bool Equals(object obj) => obj is Vec2 v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public static bool operator ==(Vec2 a, Vec2 b) => a.Equals(b);
        public static bool operator !=(Vec2 a, Vec2 b) => !a.Equals(b);

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "({0:0.######}, {1:0.######})", X, Y);
    }
}
