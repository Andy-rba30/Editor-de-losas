using System;
using System.Collections.Generic;
using Xunit;

namespace ARBA.Losas.Geometry.Tests
{
    public class PrimitivesTests
    {
        [Fact]
        public void ArcFromThreePoints_RecoversCenterRadiusAndOrientation()
        {
            Arc2 ccw = Arc2.FromThreePoints(new Vec2(1, 0), new Vec2(0, 1), new Vec2(-1, 0));
            Assert.True(ccw.Center.IsAlmostEqual(Vec2.Zero, 1e-12));
            Assert.Equal(1, ccw.Radius, 12);
            Assert.True(ccw.IsCounterClockwise);
            Assert.Equal(Math.PI, ccw.SweepAngle, 12);
            Assert.True(ccw.Start.IsAlmostEqual(new Vec2(1, 0), 1e-12));
            Assert.True(ccw.End.IsAlmostEqual(new Vec2(-1, 0), 1e-12));

            Arc2 cw = Arc2.FromThreePoints(new Vec2(1, 0), new Vec2(0, -1), new Vec2(-1, 0));
            Assert.False(cw.IsCounterClockwise);
            Assert.True(cw.MidPoint.IsAlmostEqual(new Vec2(0, -1), 1e-12));
        }

        [Fact]
        public void ArcClosestParameter_ProjectsOntoArcAndClampsToNearestEnd()
        {
            Arc2 arc = new Arc2(Vec2.Zero, 2, 0, Math.PI / 2); // de (2,0) a (0,2)
            Assert.Equal(0.5, arc.ClosestParameter(new Vec2(5, 5)), 9);
            Assert.Equal(0, arc.ClosestParameter(new Vec2(3, -0.5)), 9);
            Assert.Equal(1, arc.ClosestParameter(new Vec2(-0.5, 3)), 9);
            Assert.Equal(0, arc.ClosestParameter(new Vec2(1, -1)), 9);   // en el hueco, mas cerca del inicio
            Assert.Equal(1, arc.ClosestParameter(new Vec2(-1, 1)), 9);   // en el hueco, mas cerca del fin
        }

        [Fact]
        public void ArcTrim_ReversedParametersReverseTheArc()
        {
            Arc2 arc = new Arc2(Vec2.Zero, 1, 0, Math.PI);
            ICurve2 t = arc.Trim(0.75, 0.25);
            Assert.True(t.Start.IsAlmostEqual(arc.PointAt(0.75), 1e-12));
            Assert.True(t.End.IsAlmostEqual(arc.PointAt(0.25), 1e-12));
            Assert.False(((Arc2)t).IsCounterClockwise);
        }

        [Fact]
        public void ArcTessellation_RespectsSagittaTolerance()
        {
            Arc2 arc = new Arc2(Vec2.Zero, 3, 0, Math.PI);
            double tol = Tolerances.OneMillimeter;
            IReadOnlyList<CurvePoint> pts = arc.Tessellate(tol);
            Assert.True(pts.Count > 10);
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vec2 mid = (pts[i].Point + pts[i + 1].Point) * 0.5;
                double sagitta = arc.Radius - mid.Length;
                Assert.True(sagitta <= tol + 1e-12, "flecha " + sagitta + " mayor que la tolerancia");
            }
            Assert.Equal(0, pts[0].T);
            Assert.Equal(1, pts[pts.Count - 1].T);
        }

        [Fact]
        public void ContourSignedArea_IncludesCircularSegments()
        {
            // semicirculo cerrado con su diametro: area pi/2, antihorario
            var curves = new List<ICurve2>
            {
                Arc2.FromThreePoints(new Vec2(1, 0), new Vec2(0, 1), new Vec2(-1, 0)),
                new Segment2(new Vec2(-1, 0), new Vec2(1, 0))
            };
            var contour = new Contour(curves);
            Assert.Equal(Math.PI / 2, contour.SignedArea, 12);
            Assert.Equal(-Math.PI / 2, contour.Reversed().SignedArea, 12);

            var circle = new Contour(new ICurve2[] { new Arc2(Vec2.Zero, 2, 0, 2 * Math.PI) });
            Assert.Equal(4 * Math.PI, circle.Area, 12);
        }

        [Fact]
        public void Polygon_NormalizesOrientationAndComputesNetArea()
        {
            Polygon2 p = Polygon2.FromPoints(
                new[] { new Vec2(0, 0), new Vec2(0, 10), new Vec2(10, 10), new Vec2(10, 0) }, // horario
                new[] { new Vec2(2, 2), new Vec2(4, 2), new Vec2(4, 4), new Vec2(2, 4) });   // antihorario
            Assert.True(p.Outer.IsCounterClockwise);
            Assert.False(p.Holes[0].IsCounterClockwise);
            Assert.Equal(96, p.Area, 12);
        }

        [Fact]
        public void Contour_RejectsOpenLoops()
        {
            Assert.Throws<ArgumentException>(() => new Contour(new ICurve2[]
            {
                new Segment2(new Vec2(0, 0), new Vec2(1, 0)),
                new Segment2(new Vec2(1, 0), new Vec2(1, 1)),
                new Segment2(new Vec2(1, 1), new Vec2(0, 0.5))
            }));
        }
    }
}
