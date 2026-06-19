using Zametek.Common.ProjectPlan;
using Zametek.Graphs.Avalonia;

namespace Zametek.ViewModel.ProjectPlan
{
    // Maps the application's dash styles onto the Graphs library's own GraphDashStyle at the
    // boundary, so the library carries no dependency on the application's display models.
    internal static class GraphDashStyleMapper
    {
        public static GraphDashStyle ToGraphDashStyle(this NodeBorderDashStyle dashStyle)
        {
            return dashStyle == NodeBorderDashStyle.Dashed ? GraphDashStyle.Dashed : GraphDashStyle.Normal;
        }

        public static GraphDashStyle ToGraphDashStyle(this EdgeDashStyle dashStyle)
        {
            return dashStyle == EdgeDashStyle.Dashed ? GraphDashStyle.Dashed : GraphDashStyle.Normal;
        }
    }
}
