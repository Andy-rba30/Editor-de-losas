using System;
using System.Collections.Generic;
using Clipper2Lib;

namespace ARBA.Losas.Geometry.Internal
{
    /// <summary>Conversion entre regiones de vertices canonicos y los Paths64 de Clipper2.</summary>
    internal static class ClipperBridge
    {
        public static Path64 ToPath(Ring ring, PointRegistry reg)
        {
            var path = new Path64();
            foreach (RegistryPoint p in Region.ResolvedDistinct(ring, reg)) path.Add(p.Key);
            return path;
        }

        public static Paths64 ToPaths(Region region, PointRegistry reg)
        {
            var paths = new Paths64();
            foreach (Ring ring in region.AllRings())
            {
                Path64 path = ToPath(ring, reg);
                if (path.Count >= 3) paths.Add(path);
            }
            return paths;
        }

        /// <summary>Ejecuta una operacion booleana y devuelve cada region de salida como exterior + huecos.</summary>
        public static List<(Path64 Outer, List<Path64> Holes)> Execute(ClipType op, Paths64 subject, Paths64 clip)
        {
            var tree = new PolyTree64();
            var clipper = new Clipper64 { PreserveCollinear = true };
            clipper.AddSubject(subject);
            if (clip != null && clip.Count > 0) clipper.AddClip(clip);
            clipper.Execute(op, FillRule.NonZero, tree);
            var result = new List<(Path64, List<Path64>)>();
            Collect(tree, result);
            return result;
        }

        private static void Collect(PolyPath64 node, List<(Path64, List<Path64>)> result)
        {
            for (int i = 0; i < node.Count; i++)
            {
                PolyPath64 outer = node.Child(i);
                var holes = new List<Path64>();
                for (int j = 0; j < outer.Count; j++)
                {
                    PolyPath64 hole = outer.Child(j);
                    holes.Add(hole.Polygon);
                    // islas dentro del hueco: regiones independientes
                    Collect(hole, result);
                }
                result.Add((outer.Polygon, holes));
            }
        }

        /// <summary>Area real (sin escalar) de un exterior menos sus huecos.</summary>
        public static double Area(Path64 outer, List<Path64> holes, double scale)
        {
            double a = Math.Abs(Clipper.Area(outer));
            foreach (Path64 h in holes) a -= Math.Abs(Clipper.Area(h));
            return a / (scale * scale);
        }

        public static double Perimeter(Path64 outer, List<Path64> holes, double scale)
        {
            double p = PathLength(outer);
            foreach (Path64 h in holes) p += PathLength(h);
            return p / scale;
        }

        private static double PathLength(Path64 path)
        {
            double l = 0;
            for (int i = 0; i < path.Count; i++)
            {
                Point64 a = path[i], b = path[(i + 1) % path.Count];
                double dx = a.X - b.X, dy = a.Y - b.Y;
                l += Math.Sqrt(dx * dx + dy * dy);
            }
            return l;
        }

        /// <summary>Distancia (en unidades escaladas) de un punto a un segmento.</summary>
        public static double DistanceToSegment(Point64 p, Point64 a, Point64 b, out double t)
        {
            double ax = a.X, ay = a.Y, bx = b.X - ax, by = b.Y - ay;
            double px = p.X - ax, py = p.Y - ay;
            double len2 = bx * bx + by * by;
            t = len2 > 0 ? Math.Clamp((px * bx + py * by) / len2, 0, 1) : 0;
            double cx = px - t * bx, cy = py - t * by;
            return Math.Sqrt(cx * cx + cy * cy);
        }

        /// <summary>Punto dentro del exterior y fuera de todos los huecos.</summary>
        public static bool Contains(Path64 outer, List<Path64> holes, Point64 p)
        {
            if (Clipper.PointInPolygon(p, outer) != PointInPolygonResult.IsInside) return false;
            foreach (Path64 h in holes)
                if (Clipper.PointInPolygon(p, h) != PointInPolygonResult.IsOutside) return false;
            return true;
        }
    }
}
