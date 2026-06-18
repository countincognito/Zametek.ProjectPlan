using Avalonia;
using Shouldly;
using Xunit;

namespace Zametek.Graphs.ProjectPlan.Tests
{
    // Tests for the pure connection-axis logic that drives the interactive edge approximation: which
    // axis an edge leaves/enters along, and the hybrid (pre-drag guide + dominant-axis fallback)
    // choice. These operate only on plain structs, so no Avalonia application host is required.
    public class GraphConnectionAxisTests
    {
        private const double c_FlipRatio = 1.5;

        #region ClassifyAxis

        [Fact]
        public void ClassifyAxis_WiderThanTall_IsHorizontal()
        {
            GraphEdgeGeometry.ClassifyAxis(10.0, 5.0).ShouldBe(GraphConnectionAxis.Horizontal);
        }

        [Fact]
        public void ClassifyAxis_TallerThanWide_IsVertical()
        {
            GraphEdgeGeometry.ClassifyAxis(5.0, 10.0).ShouldBe(GraphConnectionAxis.Vertical);
        }

        [Fact]
        public void ClassifyAxis_EqualSpans_ResolvesToHorizontal()
        {
            GraphEdgeGeometry.ClassifyAxis(10.0, 10.0).ShouldBe(GraphConnectionAxis.Horizontal);
        }

        [Fact]
        public void ClassifyAxis_ZeroSpans_ResolvesToHorizontal()
        {
            GraphEdgeGeometry.ClassifyAxis(0.0, 0.0).ShouldBe(GraphConnectionAxis.Horizontal);
        }

        #endregion

        #region UsesConnectionAxes

        [Theory]
        [InlineData(GraphEdgeRoutingMode.Spline)]
        [InlineData(GraphEdgeRoutingMode.SugiyamaSplines)]
        [InlineData(GraphEdgeRoutingMode.SplineBundling)]
        [InlineData(GraphEdgeRoutingMode.Rectilinear)]
        [InlineData(GraphEdgeRoutingMode.RectilinearToCenter)]
        public void UsesConnectionAxes_AxisShapedModes_AreTrue(GraphEdgeRoutingMode mode)
        {
            GraphEdgeGeometry.UsesConnectionAxes(mode).ShouldBeTrue();
        }

        [Theory]
        [InlineData(GraphEdgeRoutingMode.StraightLine)]
        [InlineData(GraphEdgeRoutingMode.None)]
        public void UsesConnectionAxes_StraightAndNone_AreFalse(GraphEdgeRoutingMode mode)
        {
            GraphEdgeGeometry.UsesConnectionAxes(mode).ShouldBeFalse();
        }

        [Theory]
        [InlineData(GraphEdgeRoutingMode.Rectilinear)]
        [InlineData(GraphEdgeRoutingMode.RectilinearToCenter)]
        public void IsRectilinear_OrthogonalModes_AreTrue(GraphEdgeRoutingMode mode)
        {
            GraphEdgeGeometry.IsRectilinear(mode).ShouldBeTrue();
        }

        [Theory]
        [InlineData(GraphEdgeRoutingMode.Spline)]
        [InlineData(GraphEdgeRoutingMode.SugiyamaSplines)]
        [InlineData(GraphEdgeRoutingMode.SplineBundling)]
        [InlineData(GraphEdgeRoutingMode.StraightLine)]
        [InlineData(GraphEdgeRoutingMode.None)]
        public void IsRectilinear_NonOrthogonalModes_AreFalse(GraphEdgeRoutingMode mode)
        {
            // Gates the Z->L promotion + port de-confliction so the spline family is unaffected.
            GraphEdgeGeometry.IsRectilinear(mode).ShouldBeFalse();
        }

        #endregion

        #region ExitAxis / EntryAxis

        [Fact]
        public void ExitAxis_HorizontalFirstLeg_IsHorizontal()
        {
            GraphEdgeSegment first = GraphEdgeGeometry.StraightSegment(new Point(0.0, 0.0), new Point(90.0, 0.0));
            GraphEdgeGeometry.ExitAxis(first).ShouldBe(GraphConnectionAxis.Horizontal);
        }

        [Fact]
        public void ExitAxis_VerticalFirstLeg_IsVertical()
        {
            GraphEdgeSegment first = GraphEdgeGeometry.StraightSegment(new Point(0.0, 0.0), new Point(0.0, 90.0));
            GraphEdgeGeometry.ExitAxis(first).ShouldBe(GraphConnectionAxis.Vertical);
        }

        [Fact]
        public void ExitAxis_DegenerateStartTangent_FallsBackToChord()
        {
            // Control1 coincides with Start, so the start tangent is degenerate; the chord (vertical)
            // must be used instead.
            var first = new GraphEdgeSegment(
                new Point(0.0, 0.0),
                new Point(0.0, 0.0),
                new Point(0.0, 0.0),
                new Point(0.0, 90.0));
            GraphEdgeGeometry.ExitAxis(first).ShouldBe(GraphConnectionAxis.Vertical);
        }

        [Fact]
        public void EntryAxis_VerticalLastLeg_IsVertical()
        {
            GraphEdgeSegment last = GraphEdgeGeometry.StraightSegment(new Point(0.0, 0.0), new Point(0.0, 90.0));
            GraphEdgeGeometry.EntryAxis(last).ShouldBe(GraphConnectionAxis.Vertical);
        }

        [Fact]
        public void EntryAxis_DegenerateEndTangent_FallsBackToChord()
        {
            // Control2 coincides with End, so the end tangent is degenerate; the chord (horizontal)
            // must be used instead.
            var last = new GraphEdgeSegment(
                new Point(0.0, 0.0),
                new Point(90.0, 0.0),
                new Point(90.0, 0.0),
                new Point(90.0, 0.0));
            GraphEdgeGeometry.EntryAxis(last).ShouldBe(GraphConnectionAxis.Horizontal);
        }

        #endregion

        #region ResolveAxis (hybrid guide + hysteresis)

        [Fact]
        public void ResolveAxis_NoCapture_UsesDominant()
        {
            GraphEdgeGeometry.ResolveAxis(null, GraphConnectionAxis.Vertical, 10.0, 200.0, c_FlipRatio)
                .ShouldBe(GraphConnectionAxis.Vertical);
            GraphEdgeGeometry.ResolveAxis(null, GraphConnectionAxis.Horizontal, 200.0, 10.0, c_FlipRatio)
                .ShouldBe(GraphConnectionAxis.Horizontal);
        }

        [Fact]
        public void ResolveAxis_CapturedHorizontal_MildVerticalOffset_KeepsCaptured()
        {
            // dy (140) does not exceed flipRatio * dx (150), so the pre-drag horizontal side is kept.
            GraphEdgeGeometry.ResolveAxis(GraphConnectionAxis.Horizontal, GraphConnectionAxis.Vertical, 100.0, 140.0, c_FlipRatio)
                .ShouldBe(GraphConnectionAxis.Horizontal);
        }

        [Fact]
        public void ResolveAxis_CapturedHorizontal_StrongVerticalOffset_FlipsToVertical()
        {
            // dy (160) exceeds flipRatio * dx (150), so it falls back to the dominant (vertical) axis.
            GraphEdgeGeometry.ResolveAxis(GraphConnectionAxis.Horizontal, GraphConnectionAxis.Vertical, 100.0, 160.0, c_FlipRatio)
                .ShouldBe(GraphConnectionAxis.Vertical);
        }

        [Fact]
        public void ResolveAxis_CapturedVertical_MildHorizontalOffset_KeepsCaptured()
        {
            GraphEdgeGeometry.ResolveAxis(GraphConnectionAxis.Vertical, GraphConnectionAxis.Horizontal, 140.0, 100.0, c_FlipRatio)
                .ShouldBe(GraphConnectionAxis.Vertical);
        }

        [Fact]
        public void ResolveAxis_CapturedVertical_StrongHorizontalOffset_FlipsToHorizontal()
        {
            GraphEdgeGeometry.ResolveAxis(GraphConnectionAxis.Vertical, GraphConnectionAxis.Horizontal, 160.0, 100.0, c_FlipRatio)
                .ShouldBe(GraphConnectionAxis.Horizontal);
        }

        [Fact]
        public void ResolveAxis_CapturedAxisCompatibleWithArrangement_IsKept()
        {
            // A horizontal capture in a horizontal-dominant arrangement is simply kept.
            GraphEdgeGeometry.ResolveAxis(GraphConnectionAxis.Horizontal, GraphConnectionAxis.Horizontal, 200.0, 10.0, c_FlipRatio)
                .ShouldBe(GraphConnectionAxis.Horizontal);
        }

        #endregion

        #region PromoteZToL (Z -> L past the half-node threshold)

        private const double c_NodeWidth = 60.0;
        private const double c_NodeHeight = 40.0;

        [Fact]
        public void PromoteZToL_HorizontalZ_BelowThreshold_StaysZ()
        {
            // Vertical offset (15) is within half the node height (20): keep the horizontal Z.
            (GraphConnectionAxis source, GraphConnectionAxis target) = GraphEdgeGeometry.PromoteZToL(
                GraphConnectionAxis.Horizontal, GraphConnectionAxis.Horizontal, 200.0, 15.0, c_NodeWidth, c_NodeHeight);
            source.ShouldBe(GraphConnectionAxis.Horizontal);
            target.ShouldBe(GraphConnectionAxis.Horizontal);
        }

        [Fact]
        public void PromoteZToL_HorizontalZ_PastThreshold_FlipsTargetToVertical()
        {
            // Vertical offset (25) exceeds half the node height (20): enter the target vertically.
            (GraphConnectionAxis source, GraphConnectionAxis target) = GraphEdgeGeometry.PromoteZToL(
                GraphConnectionAxis.Horizontal, GraphConnectionAxis.Horizontal, 200.0, 25.0, c_NodeWidth, c_NodeHeight);
            source.ShouldBe(GraphConnectionAxis.Horizontal);
            target.ShouldBe(GraphConnectionAxis.Vertical);
        }

        [Fact]
        public void PromoteZToL_VerticalZ_BelowThreshold_StaysZ()
        {
            // Horizontal offset (25) is within half the node width (30): keep the vertical Z.
            (GraphConnectionAxis source, GraphConnectionAxis target) = GraphEdgeGeometry.PromoteZToL(
                GraphConnectionAxis.Vertical, GraphConnectionAxis.Vertical, 25.0, 200.0, c_NodeWidth, c_NodeHeight);
            source.ShouldBe(GraphConnectionAxis.Vertical);
            target.ShouldBe(GraphConnectionAxis.Vertical);
        }

        [Fact]
        public void PromoteZToL_VerticalZ_PastThreshold_FlipsTargetToHorizontal()
        {
            // Horizontal offset (35) exceeds half the node width (30): enter the target horizontally.
            (GraphConnectionAxis source, GraphConnectionAxis target) = GraphEdgeGeometry.PromoteZToL(
                GraphConnectionAxis.Vertical, GraphConnectionAxis.Vertical, 35.0, 200.0, c_NodeWidth, c_NodeHeight);
            source.ShouldBe(GraphConnectionAxis.Vertical);
            target.ShouldBe(GraphConnectionAxis.Horizontal);
        }

        [Fact]
        public void PromoteZToL_AlreadyL_HorizontalToVertical_IsUnchangedEvenPastThreshold()
        {
            // A mixed L is never re-touched, regardless of the offsets.
            (GraphConnectionAxis source, GraphConnectionAxis target) = GraphEdgeGeometry.PromoteZToL(
                GraphConnectionAxis.Horizontal, GraphConnectionAxis.Vertical, 500.0, 500.0, c_NodeWidth, c_NodeHeight);
            source.ShouldBe(GraphConnectionAxis.Horizontal);
            target.ShouldBe(GraphConnectionAxis.Vertical);
        }

        [Fact]
        public void PromoteZToL_AlreadyL_VerticalToHorizontal_IsUnchangedEvenPastThreshold()
        {
            (GraphConnectionAxis source, GraphConnectionAxis target) = GraphEdgeGeometry.PromoteZToL(
                GraphConnectionAxis.Vertical, GraphConnectionAxis.Horizontal, 500.0, 500.0, c_NodeWidth, c_NodeHeight);
            source.ShouldBe(GraphConnectionAxis.Vertical);
            target.ShouldBe(GraphConnectionAxis.Horizontal);
        }

        #endregion

        #region PreferHorizontalExit (Rule 2)

        [Fact]
        public void PreferHorizontalExit_WithHorizontalRoom_ForcesHorizontal()
        {
            // dx (100) exceeds half the node width (30): the source exits horizontally even if its
            // resolved axis was vertical.
            GraphEdgeGeometry.PreferHorizontalExit(GraphConnectionAxis.Vertical, 100.0, c_NodeWidth)
                .ShouldBe(GraphConnectionAxis.Horizontal);
        }

        [Fact]
        public void PreferHorizontalExit_NearlyStacked_KeepsResolvedAxis()
        {
            // dx (20) is within half the node width (30): no horizontal room, so the resolved axis holds.
            GraphEdgeGeometry.PreferHorizontalExit(GraphConnectionAxis.Vertical, 20.0, c_NodeWidth)
                .ShouldBe(GraphConnectionAxis.Vertical);
        }

        #endregion
    }
}
