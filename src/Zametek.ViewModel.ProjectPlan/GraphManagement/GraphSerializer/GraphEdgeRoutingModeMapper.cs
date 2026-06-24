using Zametek.Graphs.Avalonia;
using CommonEdgeRoutingMode = Zametek.Common.ProjectPlan.EdgeRoutingMode;

namespace Zametek.ViewModel.ProjectPlan
{
    // Maps between the application's persisted Common.EdgeRoutingMode and the Graphs library's own
    // GraphEdgeRoutingMode at the boundary, so the data contracts carry no dependency on the graph
    // library (and vice versa). Common.EdgeRoutingMode.Unset has no library equivalent - it means "the
    // scenario did not store a mode" and is handled by the caller (keep the per-graph preset), so it is
    // never mapped here; it falls through to the configuration preset's typical default.
    internal static class GraphEdgeRoutingModeMapper
    {
        public static GraphEdgeRoutingMode ToGraphEdgeRoutingMode(this CommonEdgeRoutingMode mode)
        {
            return mode switch
            {
                CommonEdgeRoutingMode.Spline => GraphEdgeRoutingMode.Spline,
                CommonEdgeRoutingMode.SplineBundling => GraphEdgeRoutingMode.SplineBundling,
                CommonEdgeRoutingMode.StraightLine => GraphEdgeRoutingMode.StraightLine,
                CommonEdgeRoutingMode.SugiyamaSplines => GraphEdgeRoutingMode.SugiyamaSplines,
                CommonEdgeRoutingMode.Rectilinear => GraphEdgeRoutingMode.Rectilinear,
                CommonEdgeRoutingMode.RectilinearToCenter => GraphEdgeRoutingMode.RectilinearToCenter,
                CommonEdgeRoutingMode.None => GraphEdgeRoutingMode.None,
                _ => GraphEdgeRoutingMode.SugiyamaSplines,
            };
        }

        public static CommonEdgeRoutingMode ToEdgeRoutingMode(this GraphEdgeRoutingMode mode)
        {
            return mode switch
            {
                GraphEdgeRoutingMode.Spline => CommonEdgeRoutingMode.Spline,
                GraphEdgeRoutingMode.SplineBundling => CommonEdgeRoutingMode.SplineBundling,
                GraphEdgeRoutingMode.StraightLine => CommonEdgeRoutingMode.StraightLine,
                GraphEdgeRoutingMode.SugiyamaSplines => CommonEdgeRoutingMode.SugiyamaSplines,
                GraphEdgeRoutingMode.Rectilinear => CommonEdgeRoutingMode.Rectilinear,
                GraphEdgeRoutingMode.RectilinearToCenter => CommonEdgeRoutingMode.RectilinearToCenter,
                GraphEdgeRoutingMode.None => CommonEdgeRoutingMode.None,
                _ => CommonEdgeRoutingMode.SugiyamaSplines,
            };
        }
    }
}
