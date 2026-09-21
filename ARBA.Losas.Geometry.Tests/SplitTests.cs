using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static ARBA.Losas.Geometry.Tests.TestHelpers;

namespace ARBA.Losas.Geometry.Tests
{
    public class SplitTests
    {
        private static readonly SplitOptions Default = new SplitOptions { MinArea = 0.01 };

        [Fact]
        public void Rectangle_VerticalCut_TwoRegionsWithCorrectAreas()
        {
            SplitResult r = SlabSplitter.Split(Polygon2.Rectangle(0, 0, 10, 5), new[] { Vertical(4) }, Default);
            AssertAreas(r.Regions, 20, 30);
            Assert.DoesNotContain(r.Warnings, w => w.Kind != WarningKind.ShortEdgeCollapsed);
            foreach (Polygon2 p in r.Regions) Assert.Equal(4, p.Outer.Curves.Count); // rectangulos limpios
        }

        [Fact]
        public void Rectangle_TwoCrossingCuts_FourRegions()
        {
            SplitResult r = SlabSplitter.Split(Polygon2.Rectangle(0, 0, 10, 5), new[] { Vertical(4), Horizontal(2) }, Default);
            AssertAreas(r.Regions, 8, 12, 12, 18);
        }

        [Fact]
        public void LShape_CutThroughReentrantCorner()
        {
            // la recta x + y = 8 pasa por la esquina entrante (4,4)
            var cut = new CutLine(new Vec2(0, 8), new Vec2(8, 0));
            SplitResult r = SlabSplitter.Split(LShape(), new[] { cut }, Default);
            // debajo: 32; arriba: dos piezas de 16 que solo se tocan en la esquina
            AssertAreas(r.Regions, 32, 16, 16);
            Assert.Equal(LShape().Area, TotalArea(r.Regions), 6);
        }

        [Fact]
        public void PolygonWithHole_CutThatMissesTheHole_KeepsIt()
        {
            SplitResult r = SlabSplitter.Split(SquareWithHole(), new[] { Vertical(2) }, Default);
            AssertAreas(r.Regions, 20, 76);
            Polygon2 big = r.Regions.First(p => p.Area > 50);
            Assert.Single(big.Holes);
            Assert.Equal(4, big.Holes[0].Area, 9);
        }

        [Fact]
        public void PolygonWithHole_CutThroughTheHole_SplitsIt()
        {
            SplitResult r = SlabSplitter.Split(SquareWithHole(), new[] { Vertical(5) }, Default);
            AssertAreas(r.Regions, 48, 48);
            foreach (Polygon2 p in r.Regions)
            {
                Assert.Empty(p.Holes);
                Assert.Equal(8, p.Outer.Curves.Count); // la muesca del hueco: 4 lados mas 4 del hueco partido
            }
        }

        [Fact]
        public void ArcBoundary_AreaPreservedAndCutPointOnTrueArc()
        {
            Polygon2 slab = RectangleWithArc();
            SplitResult r = SlabSplitter.Split(slab, new[] { Vertical(11) }, Default);
            Assert.Equal(2, r.Regions.Count);
            Assert.Equal(RectangleWithArcArea, TotalArea(r.Regions), 6);

            // los arcos sobreviven como Arc2, no como polilinea
            Assert.All(r.Regions, p => Assert.Contains(p.Outer.Curves, c => c is Arc2));
            Assert.All(r.Regions, p => Assert.True(p.Outer.Curves.Count <= 6));

            // los puntos de corte estan sobre el arco verdadero (centro (10, 2.5), radio 2.5)
            var center = new Vec2(10, 2.5);
            List<Vec2> cutPoints = r.Regions.SelectMany(p => p.Outer.Vertices).Where(v => Math.Abs(v.X - 11) < 0.01).ToList();
            Assert.NotEmpty(cutPoints);
            foreach (Vec2 v in cutPoints) Assert.Equal(2.5, v.DistanceTo(center), 9);

            // ambas losas comparten exactamente los mismos extremos del corte
            Polygon2 left = r.Regions.First(p => p.Outer.Vertices.Any(v => v.X < 1));
            Polygon2 right = r.Regions.First(p => !ReferenceEquals(p, left));
            foreach (Vec2 v in left.Outer.Vertices.Where(v => Math.Abs(v.X - 11) < 0.01))
                Assert.Contains(right.Outer.Vertices, w => w == v);
        }

        [Fact]
        public void NotExtended_LineThatDoesNotCross_NoSplitAndWarning()
        {
            var options = new SplitOptions { MinArea = 0.01, ExtendCutLines = false };
            var cut = new CutLine(new Vec2(4, -1), new Vec2(4, 3)); // entra pero no sale del rectangulo 10x5
            SplitResult r = SlabSplitter.Split(Polygon2.Rectangle(0, 0, 10, 5), new[] { cut }, options);
            Assert.Single(r.Regions);
            Assert.Equal(50, r.Regions[0].Area, 9);
            Assert.True(HasWarning(r.Warnings, WarningKind.CutDoesNotCross));
        }

        [Fact]
        public void NotExtended_LineThatCrossesExactly_Splits()
        {
            var options = new SplitOptions { MinArea = 0.01, ExtendCutLines = false };
            var cut = new CutLine(new Vec2(4, 0), new Vec2(4, 5)); // de borde a borde
            SplitResult r = SlabSplitter.Split(Polygon2.Rectangle(0, 0, 10, 5), new[] { cut }, options);
            AssertAreas(r.Regions, 20, 30);
        }

        [Fact]
        public void NotExtended_UShape_SegmentCoversOnlyOneArm()
        {
            var options = new SplitOptions { MinArea = 0.01, ExtendCutLines = false };
            var cut = new CutLine(new Vec2(-1, 6), new Vec2(5, 6)); // cruza el brazo izquierdo, no llega al derecho
            SplitResult r = SlabSplitter.Split(UShape(), new[] { cut }, options);
            AssertAreas(r.Regions, 12, 60);
        }

        [Fact]
        public void LineTangentToVertex_NoSplitAndWarning()
        {
            var cut = new CutLine(new Vec2(9, 6), new Vec2(11, 4)); // pasa por la esquina (10,5) sin entrar
            SplitResult r = SlabSplitter.Split(Polygon2.Rectangle(0, 0, 10, 5), new[] { cut }, Default);
            Assert.Single(r.Regions);
            Assert.Equal(50, r.Regions[0].Area, 9);
            Assert.Equal(4, r.Regions[0].Outer.Curves.Count);
            Assert.True(HasWarning(r.Warnings, WarningKind.CutDidNotDivide));
        }

        [Fact]
        public void CutCollinearWithEdge_NoSplitNoSliver()
        {
            SplitResult r = SlabSplitter.Split(Polygon2.Rectangle(0, 0, 10, 5), new[] { Vertical(10) }, Default);
            Assert.Single(r.Regions);
            Assert.Equal(50, r.Regions[0].Area, 9);
            Assert.Equal(4, r.Regions[0].Outer.Curves.Count);
            Assert.Empty(r.DiscardedSlivers);
            Assert.True(HasWarning(r.Warnings, WarningKind.CutDidNotDivide));
        }

        [Fact]
        public void CutThroughHoleVertex_SplitsCleanly()
        {
            var cut = new CutLine(new Vec2(0, 0), new Vec2(10, 10)); // diagonal por (4,4) y (6,6), vertices del hueco
            SplitResult r = SlabSplitter.Split(SquareWithHole(), new[] { cut }, Default);
            AssertAreas(r.Regions, 48, 48);
            Assert.All(r.Regions, p => Assert.Empty(p.Holes));
            Assert.Equal(96, TotalArea(r.Regions), 9);
        }

        [Fact]
        public void UShape_LineCrossingTwice_ThreeRegions()
        {
            SplitResult r = SlabSplitter.Split(UShape(), new[] { Horizontal(6) }, Default);
            AssertAreas(r.Regions, 48, 12, 12);
        }

        [Fact]
        public void Cleanup_NoSegmentShorterThanShortCurveTolerance()
        {
            // la diagonal pasa a 0.0005 (< 0.00256) de dos esquinas: esos tramos se fusionan
            var cut = new CutLine(new Vec2(0, 4.9995), new Vec2(10, 0.0005));
            SplitResult r = SlabSplitter.Split(Polygon2.Rectangle(0, 0, 10, 5), new[] { cut }, Default);
            Assert.Equal(2, r.Regions.Count);
            Assert.True(MinCurveLength(r.Regions) >= Default.ShortCurveTolerance);
            Assert.Equal(50, TotalArea(r.Regions), 6);
            Assert.All(r.Regions, p => Assert.Equal(3, p.Outer.Curves.Count));
            Assert.True(HasWarning(r.Warnings, WarningKind.ShortEdgeCollapsed));
        }

        [Fact]
        public void SliverBelowMinArea_IsDiscardedAndReported()
        {
            var options = new SplitOptions { MinArea = 1.0 };
            SplitResult r = SlabSplitter.Split(Polygon2.Rectangle(0, 0, 10, 5), new[] { Vertical(0.1) }, options);
            Assert.Single(r.Regions);
            Assert.Equal(49.5, r.Regions[0].Area, 9);
            Assert.Single(r.DiscardedSlivers);
            Assert.True(HasWarning(r.Warnings, WarningKind.SliverDiscarded));
        }

        [Fact]
        public void ArcBoundary_CutThroughArcTwice_ChordPiecesAreStraight()
        {
            // la horizontal y=2.5 corta el semicirculo por su punto mas alejado
            Polygon2 slab = RectangleWithArc();
            SplitResult r = SlabSplitter.Split(slab, new[] { Horizontal(2.5) }, Default);
            AssertAreas(r.Regions, RectangleWithArcArea / 2, RectangleWithArcArea / 2);
            foreach (Polygon2 p in r.Regions)
            {
                Assert.Single(p.Outer.Curves.OfType<Arc2>());
                Assert.Equal(4, p.Outer.Curves.Count);
            }
        }
            [Fact]
        public void FullCircle_VerticalCut_TwoHalvesWithArcs()
        {
            var circle = new Polygon2(new Contour(new ICurve2[] { new Arc2(new Vec2(5, 5), 3, 0, 2 * Math.PI) }));
            SplitResult r = SlabSplitter.Split(circle, new[] { Vertical(5) }, Default);
            double half = Math.PI * 9 / 2;
            AssertAreas(r.Regions, half, half);
            foreach (Polygon2 p in r.Regions)
            {
                Assert.Contains(p.Outer.Curves, c => c is Arc2);
                Assert.Contains(p.Outer.Curves, c => c is Segment2 s && Math.Abs(s.Length - 6) < 1e-9);
                foreach (Vec2 v in p.Outer.Vertices) Assert.Equal(3, v.DistanceTo(new Vec2(5, 5)), 9);
            }
        }

        [Fact]
        public void ArcBoundary_TwoSequentialCrossingCuts_ConsistentSharedPoints()
        {
            Polygon2 slab = RectangleWithArc();
            SplitResult r = SlabSplitter.Split(slab, new[] { Vertical(11), Horizontal(2.5) }, Default);
            Assert.Equal(4, r.Regions.Count);
            Assert.Equal(RectangleWithArcArea, TotalArea(r.Regions), 6);
            var center = new Vec2(10, 2.5);
            // los extremos del primer corte se proyectan sobre el arco verdadero (x pasa de 11 a ~11.0003) y el
            // cruce de los dos cortes hereda esa x: todo vertice a la derecha del rectangulo esta sobre el arco,
            // salvo ese cruce interior
            foreach (Vec2 v in r.Regions.SelectMany(p => p.Outer.Vertices).Where(v => v.X > 10.001 && !v.IsAlmostEqual(new Vec2(11, 2.5), 0.01)))
                Assert.Equal(2.5, v.DistanceTo(center), 9);
            // cada vertice de corte aparece exactamente igual en al menos otra region
            foreach (Polygon2 p in r.Regions)
                foreach (Vec2 v in p.Outer.Vertices.Where(v => v.X > 10.001 || Math.Abs(v.Y - 2.5) < 1e-9))
                    Assert.Contains(r.Regions.Where(q => !ReferenceEquals(q, p)), q => q.Outer.Vertices.Any(w => w == v));
        }
    }
}
