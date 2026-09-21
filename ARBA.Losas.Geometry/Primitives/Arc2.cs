using System;
using System.Collections.Generic;

namespace ARBA.Losas.Geometry
{
    /// <summary>
    /// Arco de circunferencia definido por centro, radio, angulo inicial y barrido con
    /// signo (positivo = antihorario). Un barrido de 2*pi es una circunferencia completa.
    /// </summary>
    public sealed class Arc2 : ICurve2
    {
        public Vec2 Center { get; }
        public double Radius { get; }
        public double StartAngle { get; }
        public double SweepAngle { get; }

        public Arc2(Vec2 center, double radius, double startAngle, double sweepAngle)
        {
            if (!(radius > 0)) throw new ArgumentException("El radio debe ser positivo.");
            if (sweepAngle == 0 || double.IsNaN(sweepAngle)) throw new ArgumentException("El barrido no puede ser cero.");
            if (Math.Abs(sweepAngle) > 2 * Math.PI + 1e-12) throw new ArgumentException("El barrido no puede superar una vuelta.");
            Center = center;
            Radius = radius;
            StartAngle = startAngle;
            SweepAngle = sweepAngle;
        }

        /// <summary>
        /// Arco por tres puntos: inicio, un punto intermedio sobre el arco y fin. Es la forma
        /// natural de traducir un Arc de Revit (extremos + punto medio) y garantiza que
        /// Start y End sean exactamente los puntos dados.
        /// </summary>
        public static Arc2 FromThreePoints(Vec2 start, Vec2 onArc, Vec2 end)
        {
            // circuncentro de los tres puntos
            double ax = start.X, ay = start.Y, bx = onArc.X, by = onArc.Y, cx = end.X, cy = end.Y;
            double d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
            if (Math.Abs(d) < 1e-300) throw new ArgumentException("Los tres puntos son colineales: no definen un arco.");
            double a2 = ax * ax + ay * ay, b2 = bx * bx + by * by, c2 = cx * cx + cy * cy;
            double ux = (a2 * (by - cy) + b2 * (cy - ay) + c2 * (ay - by)) / d;
            double uy = (a2 * (cx - bx) + b2 * (ax - cx) + c2 * (bx - ax)) / d;
            var center = new Vec2(ux, uy);
            double r = center.DistanceTo(start);

            double a0 = Math.Atan2(ay - uy, ax - ux);
            double aMid = Math.Atan2(by - uy, bx - ux);
            double a1 = Math.Atan2(cy - uy, cx - ux);
            // barrido antihorario de inicio a fin, y comprobar si el punto intermedio cae dentro
            double ccw = NormalizePositive(a1 - a0);
            double ccwMid = NormalizePositive(aMid - a0);
            double sweep = ccwMid <= ccw ? ccw : ccw - 2 * Math.PI;
            if (Math.Abs(sweep) < 1e-15) sweep = 2 * Math.PI; // inicio == fin: circunferencia completa
            return new Arc2(center, r, a0, sweep);
        }

        private static double NormalizePositive(double angle)
        {
            double a = angle % (2 * Math.PI);
            if (a < 0) a += 2 * Math.PI;
            return a;
        }

        public Vec2 Start => PointAt(0);
        public Vec2 End => PointAt(1);
        public Vec2 MidPoint => PointAt(0.5);
        public double Length => Radius * Math.Abs(SweepAngle);
        public bool IsCounterClockwise => SweepAngle > 0;
        public bool IsFullCircle => Math.Abs(Math.Abs(SweepAngle) - 2 * Math.PI) < 1e-12;

        public Vec2 PointAt(double t)
        {
            double a = StartAngle + t * SweepAngle;
            return new Vec2(Center.X + Radius * Math.Cos(a), Center.Y + Radius * Math.Sin(a));
        }

        public double ClosestParameter(Vec2 p)
        {
            Vec2 v = p - Center;
            if (v.LengthSquared < 1e-300) return 0;
            double ang = Math.Atan2(v.Y, v.X);
            // desplazamiento angular desde el inicio, medido en el sentido del barrido
            double delta = NormalizePositive(SweepAngle > 0 ? ang - StartAngle : StartAngle - ang);
            double sweep = Math.Abs(SweepAngle);
            if (delta <= sweep) return delta / sweep;
            // el punto cae en el hueco angular: el extremo mas cercano en angulo
            double beyondEnd = delta - sweep;
            double beforeStart = 2 * Math.PI - delta;
            return beyondEnd < beforeStart ? 1 : 0;
        }

        public ICurve2 Trim(double t0, double t1)
        {
            if (t0 == t1) throw new ArgumentException("Recorte de arco vacio.");
            return new Arc2(Center, Radius, StartAngle + t0 * SweepAngle, (t1 - t0) * SweepAngle);
        }

        public ICurve2 Reversed() => Trim(1, 0);

        /// <summary>Numero de cuerdas necesario para que la flecha no supere la tolerancia.</summary>
        public int ChordCount(double tolerance)
        {
            double step;
            if (tolerance <= 0 || tolerance >= Radius) step = Math.PI / 4;
            else step = 2 * Math.Acos(1 - tolerance / Radius);
            step = Math.Min(step, Math.PI / 2);
            int n = (int)Math.Ceiling(Math.Abs(SweepAngle) / step - 1e-9);
            return Math.Max(n, IsFullCircle ? 4 : 1);
        }

        public IReadOnlyList<CurvePoint> Tessellate(double tolerance)
        {
            int n = ChordCount(tolerance);
            var pts = new CurvePoint[n + 1];
            for (int i = 0; i <= n; i++)
            {
                double t = (double)i / n;
                pts[i] = new CurvePoint(PointAt(t), t);
            }
            return pts;
        }

        public override string ToString() =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Arc c={0} r={1:0.####} a0={2:0.####} sweep={3:0.####}", Center, Radius, StartAngle, SweepAngle);
    }
}
