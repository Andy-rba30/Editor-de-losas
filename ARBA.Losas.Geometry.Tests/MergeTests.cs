using System.Linq;
using Xunit;
using static ARBA.Losas.Geometry.Tests.TestHelpers;

namespace ARBA.Losas.Geometry.Tests
{
    public class MergeTests
    {
        private static readonly MergeOptions Default = new MergeOptions { MinArea = 0.01 };

        [Fact]
        public void AdjacentRectangles_AreaIsTheSum()
        {
            MergeResult r = SlabMerger.Merge(new[] { Polygon2.Rectangle(0, 0, 5, 5), Polygon2.Rectangle(5, 0, 10, 5) }, Default);
            Assert.Single(r.Regions);
            Assert.Equal(50, r.Regions[0].Area, 9);
            Assert.Equal(4, r.Regions[0].Outer.Curves.Count); // el borde comun desaparece y los tramos colineales se unen
            Assert.False(HasWarning(r.Warnings, WarningKind.DisconnectedRegions));
        }

        [Fact]
        public void SeparatedRectangles_TwoRegionsAndWarning()
        {
            MergeResult r = SlabMerger.Merge(new[] { Polygon2.Rectangle(0, 0, 5, 5), Polygon2.Rectangle(7, 0, 12, 5) }, Default);
            AssertAreas(r.Regions, 25, 25);
            Assert.True(HasWarning(r.Warnings, WarningKind.DisconnectedRegions));
        }

        [Fact]
        public void OverlappingRectangles_AreaWithoutDoubleCounting()
        {
            MergeResult r = SlabMerger.Merge(new[] { Polygon2.Rectangle(0, 0, 6, 5), Polygon2.Rectangle(4, 0, 10, 5) }, Default);
            Assert.Single(r.Regions);
            Assert.Equal(50, r.Regions[0].Area, 9);
        }

        [Fact]
        public void RingOfFourSlabs_ProducesInteriorHole()
        {
            var slabs = new[]
            {
                Polygon2.Rectangle(0, 0, 10, 2),   // abajo
                Polygon2.Rectangle(8, 0, 10, 10),  // derecha
                Polygon2.Rectangle(0, 8, 10, 10),  // arriba
                Polygon2.Rectangle(0, 0, 2, 10)    // izquierda
            };
            MergeResult r = SlabMerger.Merge(slabs, Default);
            Assert.Single(r.Regions);
            Assert.Single(r.Regions[0].Holes);
            Assert.Equal(64, r.Regions[0].Area, 9);
            Assert.Equal(36, r.Regions[0].Holes[0].Area, 9);
        }

        [Fact]
        public void MicrogapSmallerThanTolerance_IsClosed()
        {
            double gap = 0.0005; // 0.15 mm, menor que 1 mm
            MergeResult r = SlabMerger.Merge(new[] { Polygon2.Rectangle(0, 0, 5, 5), Polygon2.Rectangle(5 + gap, 0, 10, 5) }, Default);
            Assert.Single(r.Regions);
            Assert.True(System.Math.Abs(r.Regions[0].Area - 50) < 0.01);
            Assert.False(HasWarning(r.Warnings, WarningKind.DisconnectedRegions));
        }

        [Fact]
        public void MicrogapLargerThanTolerance_StaysSeparate()
        {
            double gap = 0.01; // 3 mm
            MergeResult r = SlabMerger.Merge(new[] { Polygon2.Rectangle(0, 0, 5, 5), Polygon2.Rectangle(5 + gap, 0, 10, 5) }, Default);
            Assert.Equal(2, r.Regions.Count);
            Assert.True(HasWarning(r.Warnings, WarningKind.DisconnectedRegions));
        }

        [Fact]
        public void MicrogapWithUnequalEdges_VertexSnapsOntoEdge()
        {
            // la losa derecha es mas alta: su vertice (5.0005, 5) no coincide con ninguno de la izquierda
            double gap = 0.0005;
            MergeResult r = SlabMerger.Merge(new[] { Polygon2.Rectangle(0, 0, 5, 8), Polygon2.Rectangle(5 + gap, 0, 10, 5) }, Default);
            Assert.Single(r.Regions);
            Assert.True(System.Math.Abs(r.Regions[0].Area - 65) < 0.01);
        }

        [Fact]
        public void AdjacentSlabsWithArc_ArcSurvives()
        {
            Polygon2 left = Polygon2.Rectangle(-5, 0, 0, 5);
            Polygon2 right = RectangleWithArc();
            MergeResult r = SlabMerger.Merge(new[] { left, right }, Default);
            Assert.Single(r.Regions);
            Assert.Equal(25 + RectangleWithArcArea, r.Regions[0].Area, 6);
            Assert.Single(r.Regions[0].Outer.Curves.OfType<Arc2>());
        }

        [Fact]
        public void HoleCoveredByAnotherSlab_Disappears()
        {
            MergeResult r = SlabMerger.Merge(new[] { SquareWithHole(), Polygon2.Rectangle(3, 3, 7, 7) }, Default);
            Assert.Single(r.Regions);
            Assert.Empty(r.Regions[0].Holes);
            Assert.Equal(100, r.Regions[0].Area, 9);
        }
    }
}
