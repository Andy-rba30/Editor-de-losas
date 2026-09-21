using System;
using System.Collections.Generic;

namespace ARBA.Losas.Geometry
{
    /// <summary>
    /// Region plana: contorno exterior (siempre antihorario) y huecos (siempre horarios).
    /// Es la representacion de una losa: el boceto de Revit tiene un loop exterior y
    /// cero o mas loops interiores.
    /// </summary>
    public sealed class Polygon2
    {
        public Contour Outer { get; }
        public IReadOnlyList<Contour> Holes { get; }

        public Polygon2(Contour outer, IReadOnlyList<Contour> holes = null)
        {
            if (outer == null) throw new ArgumentNullException(nameof(outer));
            Outer = outer.IsCounterClockwise ? outer : outer.Reversed();
            var hs = new List<Contour>();
            if (holes != null)
                foreach (Contour h in holes)
                    hs.Add(h.IsCounterClockwise ? h.Reversed() : h);
            Holes = hs;
        }

        /// <summary>Rectangulo con esquinas opuestas dadas.</summary>
        public static Polygon2 Rectangle(double x0, double y0, double x1, double y1)
        {
            return new Polygon2(Contour.FromPoints(new[]
            {
                new Vec2(x0, y0), new Vec2(x1, y0), new Vec2(x1, y1), new Vec2(x0, y1)
            }));
        }

        public static Polygon2 FromPoints(IReadOnlyList<Vec2> outer, params IReadOnlyList<Vec2>[] holes)
        {
            var hs = new List<Contour>();
            foreach (IReadOnlyList<Vec2> h in holes) hs.Add(Contour.FromPoints(h));
            return new Polygon2(Contour.FromPoints(outer), hs);
        }

        /// <summary>Area neta: exterior menos huecos.</summary>
        public double Area
        {
            get
            {
                double a = Outer.Area;
                foreach (Contour h in Holes) a -= h.Area;
                return a;
            }
        }

        /// <summary>Todos los contornos: primero el exterior, luego los huecos.</summary>
        public IEnumerable<Contour> AllContours()
        {
            yield return Outer;
            foreach (Contour h in Holes) yield return h;
        }

        public void GetBounds(out Vec2 min, out Vec2 max) => Outer.GetBounds(out min, out max);

        /// <summary>
        /// True si el punto esta dentro del exterior y fuera de los huecos. Los arcos se
        /// evaluan sobre un teselado fino; los puntos justo sobre el borde pueden caer a
        /// cualquier lado.
        /// </summary>
        public bool Contains(Vec2 p)
        {
            if (!ContourContains(Outer, p)) return false;
            foreach (Contour h in Holes)
                if (ContourContains(h, p)) return false;
            return true;
        }

        private static bool ContourContains(Contour contour, Vec2 p)
        {
            // regla par-impar sobre la polilinea del contorno
            var pts = new List<Vec2>();
            foreach (ICurve2 c in contour.Curves)
            {
                IReadOnlyList<CurvePoint> t = c.Tessellate(1e-4);
                for (int i = 0; i < t.Count - 1; i++) pts.Add(t[i].Point);
            }
            bool inside = false;
            for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
            {
                Vec2 a = pts[i], b = pts[j];
                if ((a.Y > p.Y) != (b.Y > p.Y))
                {
                    double x = (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X;
                    if (p.X < x) inside = !inside;
                }
            }
            return inside;
        }
    }
}
