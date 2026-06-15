using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    // Resolves the application-specific presentation (border/edge colour, dash style and stroke
    // weight) from the current graph settings and stamps it onto a copy of the graph model, so the
    // Graphs serializer library carries no format lookups of its own. Called immediately before a
    // serialize/layout pass, so it always reflects the latest severities and type formats even when
    // they change without a recompile.
    public static class GraphPresentationBuilder
    {
        public static VertexGraphModel ApplyPresentation(
            VertexGraphModel vertexGraph,
            GraphSettingsModel graphSettings)
        {
            ArgumentNullException.ThrowIfNull(vertexGraph);
            ArgumentNullException.ThrowIfNull(graphSettings);

            var nodeFormatLookup = new GraphNodeBorderFormatLookup(graphSettings.NodeTypeFormats);
            var edgeFormatLookup = new GraphEdgeFormatLookup(graphSettings.EdgeTypeFormats);
            var colorFormatLookup = new SlackColorFormatLookup(graphSettings.ActivitySeverities);

            List<ActivityNodeModel> nodes = [.. vertexGraph.Nodes.Select(node =>
            {
                ActivityModel activity = node.Content;
                bool isCritical = activity.IsCritical();
                bool isDummy = activity.IsDummy();
                return node with
                {
                    BorderColorHexCode = ResolveColorHexCode(activity, colorFormatLookup),
                    BorderDashStyle = nodeFormatLookup.FindGraphNodeBorderDashStyle(isCritical, isDummy),
                    BorderWeight = nodeFormatLookup.FindBorderThickness(isCritical, isDummy),
                };
            })];

            // Vertex-graph edges are always drawn as plain (non-critical, non-dummy) edges.
            List<EventEdgeModel> edges = [.. vertexGraph.Edges.Select(edge => edge with
            {
                ForegroundColorHexCode = null,
                DashStyle = edgeFormatLookup.FindGraphEdgeDashStyle(false, false),
                StrokeWeight = edgeFormatLookup.FindStrokeThickness(false, false),
            })];

            return vertexGraph with { Nodes = nodes, Edges = edges };
        }

        public static ArrowGraphModel ApplyPresentation(
            ArrowGraphModel arrowGraph,
            GraphSettingsModel graphSettings)
        {
            ArgumentNullException.ThrowIfNull(arrowGraph);
            ArgumentNullException.ThrowIfNull(graphSettings);

            var nodeFormatLookup = new GraphNodeBorderFormatLookup(graphSettings.NodeTypeFormats);
            var edgeFormatLookup = new GraphEdgeFormatLookup(graphSettings.EdgeTypeFormats);
            var colorFormatLookup = new SlackColorFormatLookup(graphSettings.ActivitySeverities);

            // Arrow-graph nodes (events) are always drawn as plain black-bordered nodes; a null
            // colour lets the serializer fall back to its default border colour.
            List<EventNodeModel> nodes = [.. arrowGraph.Nodes.Select(node => node with
            {
                BorderColorHexCode = null,
                BorderDashStyle = nodeFormatLookup.FindGraphNodeBorderDashStyle(false, false),
                BorderWeight = nodeFormatLookup.FindBorderThickness(false, false),
            })];

            List<ActivityEdgeModel> edges = [.. arrowGraph.Edges.Select(edge =>
            {
                ActivityModel activity = edge.Content;
                bool isCritical = activity.IsCritical();
                bool isDummy = activity.IsDummy();
                return edge with
                {
                    ForegroundColorHexCode = ResolveColorHexCode(activity, colorFormatLookup),
                    DashStyle = edgeFormatLookup.FindGraphEdgeDashStyle(isCritical, isDummy),
                    StrokeWeight = edgeFormatLookup.FindStrokeThickness(isCritical, isDummy),
                    // Stamp the activity-state flags so the (now library-side) serializer can build
                    // the edge label without the application's IsCritical()/IsDummy() rules.
                    IsCritical = isCritical,
                    IsDummy = isDummy,
                };
            })];

            return arrowGraph with { Nodes = nodes, Edges = edges };
        }

        // The activity's own override colour wins; otherwise the colour comes from its total slack.
        private static string ResolveColorHexCode(ActivityModel activity, SlackColorFormatLookup colorFormatLookup)
        {
            return activity.OverrideColor
                ? ColorHelper.ColorFormatToHtmlHexCode(activity.ColorFormat)
                : ColorHelper.ColorFormatToHtmlHexCode(colorFormatLookup.FindSlackColorFormat(activity.TotalSlack));
        }
    }
}
