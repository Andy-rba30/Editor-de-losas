using System;
using System.Collections.Generic;

namespace ARBA.Losas.Geometry
{
    /// <summary>
    /// Contorno cerrado formado por curvas consecutivas (el fin de cada una coincide con
    /// el inicio de la siguiente). El area con signo es exacta, incluyendo los arcos.
    /// </summary>
    public sealed class Contour
    {
        public IReadOnlyList<ICurve2> Curves { get; }

        public Contour(IReadOnlyList<ICurve2> curves)
        {
            if (curves == null || curves.Count == 0) throw new ArgumentException("Un contorno necesita al menos una curva.");
            for (int i = 0; i < curves.Count; i++)
            {
                ICurve2 a = curves[i];
                ICurve2 b = curves[(i + 1) % curves.Count];
                if (!a.End.IsAlmostEqual(b.Start, Tolerances.Closure))
                    throw new ArgumentException("El contorno no esta cerrado entre la curva " + i + " y la " + ((i + 1) % curves.Count) +
                                                ": " + a.End + " frente a " + b.Start + ".");
            }
            if (curves.Count == 1 && !(curves[0] is Arc2 arc && arc.IsFullCircle))
                throw new ArgumentException("Un contorno de una sola curva solo puede ser una circunferencia completa.");
            Curves = curves;
        }

        /// <summary>Contorno poligonal a partir de una lista de vertices (se cierra solo).</summary>
        public static Contour FromPoints(IReadOnlyList<Vec2> points)
        {
            if (points == null || points.Count < 3) throw new ArgumentException("Hacen falta al menos tres puntos.");
            var curves = new List<ICurve2>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                Vec2 a = points[i], b = points[(i + 1) % points.Count];
                if (a != b) curves.Add(new Segment2(a, b));
            }
            return new Contour(curves);
        }

        /// <summary>Area con signo: positiva si el contorno gira en sentido antihorario.</summary>
        public double SignedArea
        {
            get
            {
                // formula del cordon sobre los extremos de cada curva, mas el segmento circular de cada arco
                double sum = 0;
                foreach (ICurve2 c in Curves)
                {
                    Vec2 a = c.Start, b = c.End;
                    sum += a.X * b.Y - b.X * a.Y;
                    if (c is Arc2 arc)
                    {
                        double theta = Math.Abs(arc.SweepAngle);
                        double segment = 0.5 * arc.Radius * arc.Radius * (theta - Math.Sin(theta));
                        sum += 2 * (arc.SweepAngle > 0 ? segment : -segment);
                    }
                }
                return 0.5 * sum;
            }
        }

        public double Area => Math.Abs(SignedArea);
        public bool IsCounterClockwise => SignedArea > 0;

        public Contour Reversed()
        {
            var rev = new List<ICurve2>(Curves.Count);
            for (int i = Curves.Count - 1; i >= 0; i--) rev.Add(Curves[i].Reversed());
            return new Contour(rev);
        }

        /// <summary>Puntos de inicio de cada curva.</summary>
        public IReadOnlyList<Vec2> Vertices
        {
            get
            {
                var v = new Vec2[Curves.Count];
                for (int i = 0; i < Curves.Count; i++) v[i] = Curves[i].Start;
                return v;
            }
        }

        public double Perimeter
        {
            get { double p = 0; foreach (ICurve2 c in Curves) p += c.Length; return p; }
        }

        /// <summary>Caja envolvente calculada sobre un teselado fino.</summary>
        public void GetBounds(out Vec2 min, out Vec2 max)
        {
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (ICurve2 c in Curves)
                foreach (CurvePoint p in c.Tessellate(1e-4))
                {
                    x0 = Math.Min(x0, p.Point.X); y0 = Math.Min(y0, p.Point.Y);
                    x1 = Math.Max(x1, p.Point.X); y1 = Math.Max(y1, p.Point.Y);
                }
            min = new Vec2(x0, y0);
            max = new Vec2(x1, y1);
        }
    }
}
