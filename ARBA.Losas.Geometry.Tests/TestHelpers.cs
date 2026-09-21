using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ARBA.Losas.Geometry.Tests
{
    internal static class TestHelpers
    {
        public const double AreaTol = 1e-6;

        public static double TotalArea(IEnumerable<Polygon2> regions) => regions.Sum(r => r.Area);

        public static void AssertAreas(IReadOnlyList<Polygon2> regions, params double[] expected)
        {
            Assert.Equal(expected.Length, regions.Count);
            List<double> got = regions.Select(r => r.Area).OrderBy(a => a).ToList();
            List<double> want = expected.OrderBy(a => a).ToList();
            for (int i = 0; i < want.Count; i++)
                Assert.True(Math.Abs(got[i] - want[i]) < 1e-4, "Area " + i + ": esperada " + want[i] + ", obtenida " + got[i]);
        }

        /// <summary>Longitud minima de todas las curvas de todos los contornos.</summary>
        public static double MinCurveLength(IEnumerable<Polygon2> regions) =>
            regions.SelectMany(r => r.AllContours()).SelectMany(c => c.Curves).Min(c => c.Length);

        public static bool HasWarning(IEnumerable<GeometryWarning> warnings, WarningKind kind) => warnings.Any(w => w.Kind == kind);

        /// <summary>Losa en L: cuadrado 10x10 sin la esquina superior derecha (6x6).</summary>
        public static Polygon2 LShape() => Polygon2.FromPoints(new[]
        {
            new Vec2(0, 0), new Vec2(10, 0), new Vec2(10, 4), new Vec2(4, 4), new Vec2(4, 10), new Vec2(0, 10)
        });

        /// <summary>Losa en U: cuadrado 10x10 con una ranura de 4 de ancho abierta arriba, desde y=3.</summary>
        public static Polygon2 UShape() => Polygon2.FromPoints(new[]
        {
            new Vec2(0, 0), new Vec2(10, 0), new Vec2(10, 10), new Vec2(7, 10), new Vec2(7, 3), new Vec2(3, 3), new Vec2(3, 10), new Vec2(0, 10)
        });

        /// <summary>Rectangulo 10x5 con un semicirculo de radio 2.5 en el lado derecho.</summary>
        public static Polygon2 RectangleWithArc()
        {
            var curves = new List<ICurve2>
            {
                new Segment2(new Vec2(0, 0), new Vec2(10, 0)),
                Arc2.FromThreePoints(new Vec2(10, 0), new Vec2(12.5, 2.5), new Vec2(10, 5)),
                new Segment2(new Vec2(10, 5), new Vec2(0, 5)),
                new Segment2(new Vec2(0, 5), new Vec2(0, 0))
            };
            return new Polygon2(new Contour(curves));
        }

        public static double RectangleWithArcArea => 50 + Math.PI * 2.5 * 2.5 / 2;

        /// <summary>Cuadrado 10x10 con hueco cuadrado de (4,4) a (6,6).</summary>
        public static Polygon2 SquareWithHole() => Polygon2.FromPoints(
            new[] { new Vec2(0, 0), new Vec2(10, 0), new Vec2(10, 10), new Vec2(0, 10) },
            new[] { new Vec2(4, 4), new Vec2(6, 4), new Vec2(6, 6), new Vec2(4, 6) });

        public static CutLine Vertical(double x) => new CutLine(new Vec2(x, -100), new Vec2(x, 100));
        public static CutLine Horizontal(double y) => new CutLine(new Vec2(-100, y), new Vec2(100, y));
    }
}
