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

        #region Detour shapes (Bracket / Saucepan) via RouteCorners

        [Fact]
        public void Bracket_Vertical_CrossLegSlidAboveLevelNodes()
        {
            // Two nodes on one row; a vertical-sided bracket lifts the cross leg above both (corner at
            // y = -50, outside the [-20, +20] span of their tops/bottoms) and attaches on the top sides.
            var plan = new GraphRoutePlan(
                GraphConnectionAxis.Vertical, GraphConnectionAxis.Vertical, GraphRouteShape.Bracket, -50.0);
            IReadOnlyList<Point> corners = GraphEdgeGeometry.RouteCorners(
                new Point(0.0, 0.0), new Point(100.0, 0.0), 40.0, 40.0, plan);

            corners.Count.ShouldBe(4);
            ShouldBePoint(corners[0], 0.0, -20.0);
            ShouldBePoint(corners[1], 0.0, -50.0);
            ShouldBePoint(corners[2], 100.0, -50.0);
            ShouldBePoint(corners[3], 100.0, -20.0);
        }

        [Fact]
        public void Bracket_Horizontal_CrossLegSlidLeftOfStackedNodes()
        {
            // Two stacked nodes; a horizontal-sided bracket slides the cross leg left of both (x = -50)
            // and attaches on the left sides.
            var plan = new GraphRoutePlan(
                GraphConnectionAxis.Horizontal, GraphConnectionAxis.Horizontal, GraphRouteShape.Bracket, -50.0);
            IReadOnlyList<Point> corners = GraphEdgeGeometry.RouteCorners(
                new Point(0.0, 0.0), new Point(0.0, 100.0), 40.0, 40.0, plan);

            corners.Count.ShouldBe(4);
            ShouldBePoint(corners[0], -20.0, 0.0);
            ShouldBePoint(corners[1], -50.0, 0.0);
            ShouldBePoint(corners[2], -50.0, 100.0);
            ShouldBePoint(corners[3], -20.0, 100.0);
        }

        [Fact]
        public void Saucepan_HandleAtSourceOnly_HandleThenBowlIntoTargetSide()
        {
            // Horizontal bowl; source is a handled end (axis H), target a direct end (axis V). Handle off
            // the source's right side, default half-node stub, bowl dips to y = -60, up into target bottom.
            var plan = new GraphRoutePlan(
                GraphConnectionAxis.Horizontal, GraphConnectionAxis.Vertical, GraphRouteShape.Saucepan, -60.0);
            IReadOnlyList<Point> corners = GraphEdgeGeometry.RouteCorners(
                new Point(0.0, 0.0), new Point(200.0, 0.0), 40.0, 40.0, plan);

            corners.Count.ShouldBe(5);
            ShouldBePoint(corners[0], 20.0, 0.0);    // source right side
            ShouldBePoint(corners[1], 40.0, 0.0);    // handle turn
            ShouldBePoint(corners[2], 40.0, -60.0);  // dip to the bowl
            ShouldBePoint(corners[3], 200.0, -60.0); // bowl run to the target column
            ShouldBePoint(corners[4], 200.0, -20.0); // up into the target's bottom
        }

        [Fact]
        public void Saucepan_HandleAtTargetOnly_RunsSourceToTargetWithHandleAtTheEnd()
        {
            // The mirror: source direct (axis V), target handled (axis H); the list still runs src -> tgt.
            var plan = new GraphRoutePlan(
                GraphConnectionAxis.Vertical, GraphConnectionAxis.Horizontal, GraphRouteShape.Saucepan, -60.0);
            IReadOnlyList<Point> corners = GraphEdgeGeometry.RouteCorners(
                new Point(0.0, 0.0), new Point(200.0, 0.0), 40.0, 40.0, plan);

            corners.Count.ShouldBe(5);
            ShouldBePoint(corners[0], 0.0, -20.0);   // up off the source's top
            ShouldBePoint(corners[1], 0.0, -60.0);
            ShouldBePoint(corners[2], 160.0, -60.0);
            ShouldBePoint(corners[3], 160.0, 0.0);
            ShouldBePoint(corners[4], 180.0, 0.0);   // handle into the target's left side
        }

        [Fact]
        public void Saucepan_HandleAtBothEnds_HorizontalInAndOutAroundTheBowl()
        {
            // Both ends handled (axis H, horizontal bowl): a handle off each side, both arms diving to the
            // bowl at y = -60 - four bends, horizontal entry and exit. The both-ends case the user hit.
            var plan = new GraphRoutePlan(
                GraphConnectionAxis.Horizontal, GraphConnectionAxis.Horizontal, GraphRouteShape.Saucepan, -60.0);
            IReadOnlyList<Point> corners = GraphEdgeGeometry.RouteCorners(
                new Point(0.0, 0.0), new Point(200.0, 0.0), 40.0, 40.0, plan);

            corners.Count.ShouldBe(6);
            ShouldBePoint(corners[0], 20.0, 0.0);    // source right side (handle)
            ShouldBePoint(corners[1], 40.0, 0.0);    // source handle turn
            ShouldBePoint(corners[2], 40.0, -60.0);  // source arm down to the bowl
            ShouldBePoint(corners[3], 160.0, -60.0); // bowl run to the target arm
            ShouldBePoint(corners[4], 160.0, 0.0);   // target arm up
            ShouldBePoint(corners[5], 180.0, 0.0);   // handle into the target's left side
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
