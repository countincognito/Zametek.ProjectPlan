using Avalonia;
using Shouldly;
using Xunit;

namespace Zametek.Graphs.ProjectPlan.Tests
{
    // Tests for the client-side interactive edge geometry: the rectilinear (Z / L) and spline
    // connector shapes built from the per-endpoint connection axes, the tip walk-back used to aim the
    // arrowhead, the label midpoint, and the side-centre attach point. Pure struct maths - no Avalonia
    // application host required.
    public class GraphEdgeGeometryTests
    {
        private const double c_Tol = 1e-9;

        private static void ShouldBePoint(Point actual, double x, double y)
        {
            actual.X.ShouldBe(x, c_Tol);
            actual.Y.ShouldBe(y, c_Tol);
        }

        #region Rectilinear (orthogonal) shapes

        [Fact]
        public void Rectilinear_BothHorizontal_IsZThroughHorizontalMidpoint()
        {
            var start = new Point(0.0, 0.0);
            var end = new Point(100.0, 40.0);
            IReadOnlyList<GraphEdgeSegment> segments = GraphEdgeGeometry.BuildSegments(
                GraphEdgeRoutingMode.Rectilinear, start, end,
                GraphConnectionAxis.Horizontal, GraphConnectionAxis.Horizontal);

            segments.Count.ShouldBe(3);
            // Leave horizontally to midX, turn vertically, arrive horizontally.
            ShouldBePoint(segments[0].Start, 0.0, 0.0);
            ShouldBePoint(segments[0].End, 50.0, 0.0);
            ShouldBePoint(segments[1].End, 50.0, 40.0);
            ShouldBePoint(segments[2].End, 100.0, 40.0);
        }

        [Fact]
        public void Rectilinear_BothVertical_IsZThroughVerticalMidpoint()
        {
            var start = new Point(0.0, 0.0);
            var end = new Point(40.0, 100.0);
            IReadOnlyList<GraphEdgeSegment> segments = GraphEdgeGeometry.BuildSegments(
                GraphEdgeRoutingMode.Rectilinear, start, end,
                GraphConnectionAxis.Vertical, GraphConnectionAxis.Vertical);

            segments.Count.ShouldBe(3);
            // Leave vertically to midY, turn horizontally, arrive vertically.
            ShouldBePoint(segments[0].End, 0.0, 50.0);
            ShouldBePoint(segments[1].End, 40.0, 50.0);
            ShouldBePoint(segments[2].End, 40.0, 100.0);
        }

        [Fact]
        public void Rectilinear_HorizontalThenVertical_IsLWithSingleCorner()
        {
            var start = new Point(0.0, 0.0);
            var end = new Point(100.0, 40.0);
            IReadOnlyList<GraphEdgeSegment> segments = GraphEdgeGeometry.BuildSegments(
                GraphEdgeRoutingMode.Rectilinear, start, end,
                GraphConnectionAxis.Horizontal, GraphConnectionAxis.Vertical);

            segments.Count.ShouldBe(2);
            // Corner level with the source, above/below the target.
            ShouldBePoint(segments[0].End, 100.0, 0.0);
            ShouldBePoint(segments[1].End, 100.0, 40.0);
        }

        [Fact]
        public void Rectilinear_VerticalThenHorizontal_IsLWithSingleCorner()
        {
            var start = new Point(0.0, 0.0);
            var end = new Point(100.0, 40.0);
            IReadOnlyList<GraphEdgeSegment> segments = GraphEdgeGeometry.BuildSegments(
                GraphEdgeRoutingMode.Rectilinear, start, end,
                GraphConnectionAxis.Vertical, GraphConnectionAxis.Horizontal);

            segments.Count.ShouldBe(2);
            // Corner above/below the source, level with the target.
            ShouldBePoint(segments[0].End, 0.0, 40.0);
            ShouldBePoint(segments[1].End, 100.0, 40.0);
        }

        [Fact]
        public void Rectilinear_EndpointsLevel_CollapsesToStraightSegment()
        {
            IReadOnlyList<GraphEdgeSegment> segments = GraphEdgeGeometry.BuildSegments(
                GraphEdgeRoutingMode.Rectilinear, new Point(0.0, 10.0), new Point(100.0, 10.0),
                GraphConnectionAxis.Horizontal, GraphConnectionAxis.Horizontal);

            segments.Count.ShouldBe(1);
            ShouldBePoint(segments[0].Start, 0.0, 10.0);
            ShouldBePoint(segments[0].End, 100.0, 10.0);
        }

        [Fact]
        public void Rectilinear_EndpointsAligned_CollapsesToStraightSegment()
        {
            IReadOnlyList<GraphEdgeSegment> segments = GraphEdgeGeometry.BuildSegments(
                GraphEdgeRoutingMode.Rectilinear, new Point(10.0, 0.0), new Point(10.0, 100.0),
                GraphConnectionAxis.Vertical, GraphConnectionAxis.Vertical);

            segments.Count.ShouldBe(1);
            ShouldBePoint(segments[0].End, 10.0, 100.0);
        }

        #endregion

        #region Spline connector shapes

        [Fact]
        public void Spline_BothHorizontalAndLevel_IsStraightLineAlongChord()
        {
            IReadOnlyList<GraphEdgeSegment> segments = GraphEdgeGeometry.BuildSegments(
                GraphEdgeRoutingMode.Spline, new Point(0.0, 0.0), new Point(100.0, 0.0),
                GraphConnectionAxis.Horizontal, GraphConnectionAxis.Horizontal);

            segments.Count.ShouldBe(1);
            // Both controls collapse onto the chord (midX, 0), giving a straight line.
            ShouldBePoint(segments[0].Control1, 50.0, 0.0);
            ShouldBePoint(segments[0].Control2, 50.0, 0.0);
        }

        [Fact]
        public void Spline_BothVertical_ControlsAtVerticalMidpoint()
        {
            IReadOnlyList<GraphEdgeSegment> segments = GraphEdgeGeometry.BuildSegments(
                GraphEdgeRoutingMode.Spline, new Point(0.0, 0.0), new Point(0.0, 100.0),
                GraphConnectionAxis.Vertical, GraphConnectionAxis.Vertical);

            segments.Count.ShouldBe(1);
            ShouldBePoint(segments[0].Control1, 0.0, 50.0);
            ShouldBePoint(segments[0].Control2, 0.0, 50.0);
        }

        [Fact]
        public void Spline_HorizontalSourceVerticalTarget_MixesControlAxes()
        {
            IReadOnlyList<GraphEdgeSegment> segments = GraphEdgeGeometry.BuildSegments(
                GraphEdgeRoutingMode.Spline, new Point(0.0, 0.0), new Point(100.0, 40.0),
                GraphConnectionAxis.Horizontal, GraphConnectionAxis.Vertical);

            segments.Count.ShouldBe(1);
            // Source control follows the horizontal midpoint at the source height; target control the
            // target X at the vertical midpoint.
            ShouldBePoint(segments[0].Control1, 50.0, 0.0);
            ShouldBePoint(segments[0].Control2, 100.0, 20.0);
        }

        #endregion

        #region Straight / None (axis-independent)

        [Theory]
        [InlineData(GraphEdgeRoutingMode.StraightLine)]
        [InlineData(GraphEdgeRoutingMode.None)]
        public void StraightAndNone_IgnoreAxes_AndPlaceControlsOnChord(GraphEdgeRoutingMode mode)
        {
            IReadOnlyList<GraphEdgeSegment> segments = GraphEdgeGeometry.BuildSegments(
                mode, new Point(0.0, 0.0), new Point(90.0, 30.0),
                GraphConnectionAxis.Vertical, GraphConnectionAxis.Vertical);

            segments.Count.ShouldBe(1);
            ShouldBePoint(segments[0].Control1, 30.0, 10.0);
            ShouldBePoint(segments[0].Control2, 60.0, 20.0);
        }

        #endregion

        #region PointOnCubic / Midpoint

        [Fact]
        public void PointOnCubic_OfStraightSegment_IsLinearInParameter()
        {
            GraphEdgeSegment s = GraphEdgeGeometry.StraightSegment(new Point(0.0, 0.0), new Point(90.0, 0.0));
            ShouldBePoint(GraphEdgeGeometry.PointOnCubic(s.Start, s.Control1, s.Control2, s.End, 0.0), 0.0, 0.0);
            ShouldBePoint(GraphEdgeGeometry.PointOnCubic(s.Start, s.Control1, s.Control2, s.End, 0.5), 45.0, 0.0);
            ShouldBePoint(GraphEdgeGeometry.PointOnCubic(s.Start, s.Control1, s.Control2, s.End, 1.0), 90.0, 0.0);
        }

        [Fact]
        public void Midpoint_OfThreeSegmentZ_LandsOnMiddleVerticalRun()
        {
            IReadOnlyList<GraphEdgeSegment> segments = GraphEdgeGeometry.BuildSegments(
                GraphEdgeRoutingMode.Rectilinear, new Point(0.0, 0.0), new Point(100.0, 40.0),
                GraphConnectionAxis.Horizontal, GraphConnectionAxis.Horizontal);

            // Middle (index 1) segment runs (50,0)->(50,40); its centre is (50,20).
            ShouldBePoint(GraphEdgeGeometry.Midpoint(segments), 50.0, 20.0);
        }

        #endregion

        #region AnchorBeforeEnd (arrowhead walk-back)

        [Fact]
        public void AnchorBeforeEnd_HorizontalLine_PointsBackHorizontally()
        {
            GraphEdgeSegment s = GraphEdgeGeometry.StraightSegment(new Point(0.0, 0.0), new Point(100.0, 0.0));
            Point anchor = GraphEdgeGeometry.AnchorBeforeEnd([s], 14.0);

            anchor.Y.ShouldBe(0.0, c_Tol);
            anchor.X.ShouldBeLessThan(100.0);
            // The tip-to-anchor direction is horizontal.
            GraphEdgeGeometry.ClassifyAxis(Math.Abs(100.0 - anchor.X), Math.Abs(0.0 - anchor.Y))
                .ShouldBe(GraphConnectionAxis.Horizontal);
        }

        [Fact]
        public void AnchorBeforeEnd_TinyHorizontalNubOnVerticalLeg_AbsorbsNubAndPointsVertically()
        {
            // The case the walk-back exists for: a long vertical leg ending in a tiny horizontal nub
            // (a near-vertical rectilinear/spline edge meeting the node). The arrowhead must follow the
            // vertical leg, not the nub.
            GraphEdgeSegment verticalLeg = GraphEdgeGeometry.StraightSegment(new Point(0.0, 0.0), new Point(0.0, 100.0));
            GraphEdgeSegment nub = GraphEdgeGeometry.StraightSegment(new Point(0.0, 100.0), new Point(3.0, 100.0));
            Point tip = new(3.0, 100.0);

            Point anchor = GraphEdgeGeometry.AnchorBeforeEnd([verticalLeg, nub], 14.0);

            // Anchor sits back on the vertical leg (X back to 0), so the direction is vertical-dominant.
            anchor.X.ShouldBe(0.0, c_Tol);
            GraphEdgeGeometry.ClassifyAxis(Math.Abs(tip.X - anchor.X), Math.Abs(tip.Y - anchor.Y))
                .ShouldBe(GraphConnectionAxis.Vertical);
        }

        [Fact]
        public void AnchorBeforeEnd_PathShorterThanSpan_ReturnsStart()
        {
            GraphEdgeSegment s = GraphEdgeGeometry.StraightSegment(new Point(0.0, 0.0), new Point(5.0, 0.0));
            Point anchor = GraphEdgeGeometry.AnchorBeforeEnd([s], 14.0);
            ShouldBePoint(anchor, 0.0, 0.0);
        }

        #endregion

        #region AttachPoint (side-centre re-centring)

        [Fact]
        public void AttachPoint_Horizontal_ChoosesFacingSideCentre()
        {
            var centre = new Point(50.0, 50.0);
            // Toward the right -> right-edge centre; toward the left -> left-edge centre.
            ShouldBePoint(GraphEdgeGeometry.AttachPoint(centre, 20.0, 10.0, GraphConnectionAxis.Horizontal, new Point(200.0, 50.0)), 60.0, 50.0);
            ShouldBePoint(GraphEdgeGeometry.AttachPoint(centre, 20.0, 10.0, GraphConnectionAxis.Horizontal, new Point(0.0, 50.0)), 40.0, 50.0);
        }

        [Fact]
        public void AttachPoint_Vertical_ChoosesFacingSideCentre()
        {
            var centre = new Point(50.0, 50.0);
            // Toward below -> bottom-edge centre; toward above -> top-edge centre.
            ShouldBePoint(GraphEdgeGeometry.AttachPoint(centre, 20.0, 10.0, GraphConnectionAxis.Vertical, new Point(50.0, 200.0)), 50.0, 55.0);
            ShouldBePoint(GraphEdgeGeometry.AttachPoint(centre, 20.0, 10.0, GraphConnectionAxis.Vertical, new Point(50.0, 0.0)), 50.0, 45.0);
        }

        #endregion
    }
}
