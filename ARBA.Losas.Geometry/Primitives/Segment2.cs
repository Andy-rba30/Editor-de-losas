using System;
using System.Collections.Generic;

namespace ARBA.Losas.Geometry
{
    /// <summary>Segmento recto.</summary>
    public sealed class Segment2 : ICurve2
    {
        public Vec2 Start { get; }
        public Vec2 End { get; }
        public CurveOrigin Origin { get; }

        public Segment2(Vec2 start, Vec2 end) : this(start, end, null) { }

        public Segment2(Vec2 start, Vec2 end, CurveOrigin origin)
        {
            if (start == end) throw new ArgumentException("Un segmento necesita dos puntos distintos.");
            Start = start;
            End = end;
            Origin = origin;
        }

        public Vec2 Direction => (End - Start).Normalized();
        public double Length => (End - Start).Length;

        public Vec2 PointAt(double t) => Start + (End - Start) * t;

        public double ClosestParameter(Vec2 p)
        {
            Vec2 d = End - Start;
            double t = (p - Start).Dot(d) / d.LengthSquared;
            return Math.Clamp(t, 0, 1);
        }

        /// <summary>Distancia de un punto al segmento (no a la recta).</summary>
        public double DistanceTo(Vec2 p) => PointAt(ClosestParameter(p)).DistanceTo(p);

        public ICurve2 Trim(double t0, double t1) => new Segment2(PointAt(t0), PointAt(t1));

        public ICurve2 Reversed() => new Segment2(End, Start);

        public IReadOnlyList<CurvePoint> Tessellate(double tolerance) =>
            new[] { new CurvePoint(Start, 0), new CurvePoint(End, 1) };

        public override string ToString() => "Segment " + Start + " -> " + End;
    }
}
