using Avalonia.Media;
using System.Text;
using Zametek.Common.ProjectPlan;
using Zametek.Graphs.Avalonia;

namespace Zametek.ViewModel.ProjectPlan
{
    // Builds the library-neutral DiagramGraphModel (what to draw) from the application's
    // VertexGraphModel (presentation already resolved by GraphPresentationBuilder). This is the
    // Common -> Graphs boundary for the vertex graph: structural validation, the node label (the
    // ES/Id/EF | LS/Duration/LF box), the hover tooltip and the Common -> GraphDashStyle mapping all
    // happen here, so the Graphs serializer consumes only its own DiagramGraphModel and carries no
    // dependency on the application's domain models. Validation strings live in Resource.ProjectPlan
    // (the library carries no application-domain strings).
    internal static class VertexGraphDiagramBuilder
    {
        private const double c_DiagramNodeHeight = 60.0;
        private const double c_DiagramNodeWidth = 86.0;

        // The raw stroke weight is scaled to a comfortable on-diagram thickness.
        private const double c_NodeLineThicknessCorrectionFactor = 1.5;

        private static readonly string s_NodeFillColorHexCode = ColorHelper.ColorToHtmlHexCode(Colors.LightGray);
        private static readonly string s_NodeBorderColorHexCode = ColorHelper.ColorToHtmlHexCode(Colors.Black);

        public static DiagramGraphModel Build(VertexGraphModel vertexGraph)
        {
            ArgumentNullException.ThrowIfNull(vertexGraph);

            IList<ActivityNodeModel> nodeModels = vertexGraph.Nodes;

            var edgeHeadNodeLookup = new Dictionary<int, int>();
            var edgeTailNodeLookup = new Dictionary<int, int>();
            var drawingGraphNodeIds = new List<int>();

            foreach (ActivityNodeModel node in nodeModels)
            {
                int nodeId = node.Content.Id;
                drawingGraphNodeIds.Add(nodeId);

                foreach (int edgeId in node.IncomingEdges)
                {
                    edgeHeadNodeLookup.Add(edgeId, nodeId);
                }
                foreach (int edgeId in node.OutgoingEdges)
                {
                    edgeTailNodeLookup.Add(edgeId, nodeId);
                }
            }

            // Check all edges are used.
            IList<EventEdgeModel> edgeModels = vertexGraph.Edges;
            IEnumerable<int> edgeIds = edgeModels.Select(x => x.Content.Id);

            if (!edgeIds.OrderBy(x => x).SequenceEqual(edgeHeadNodeLookup.Keys.OrderBy(x => x)))
            {
                throw new ArgumentException(Resource.ProjectPlan.Messages.Message_MismatchedEdgeIdsForHeadNodesInVertexGraph);
            }
            if (!edgeIds.OrderBy(x => x).SequenceEqual(edgeTailNodeLookup.Keys.OrderBy(x => x)))
            {
                throw new ArgumentException(Resource.ProjectPlan.Messages.Message_MismatchedEdgeIdsForTailNodesInVertexGraph);
            }

            // Check that the nodes referenced by edges are a subset of the drawn nodes.
            HashSet<int> edgeNodeLookupIds = [.. edgeHeadNodeLookup.Values.Union(edgeTailNodeLookup.Values)];

            if (edgeNodeLookupIds.Count != 0
                && !edgeNodeLookupIds.IsSubsetOf(drawingGraphNodeIds))
            {
                throw new ArgumentException(Resource.ProjectPlan.Messages.Message_MismatchedNodeIdsAssociatedWithEdgesInVertexGraph);
            }

            List<DiagramNodeModel> diagramNodeModels = [.. nodeModels.Select(BuildDiagramNode)];
            List<DiagramEdgeModel> diagramEdgeModels = [];

            foreach (EventEdgeModel eventEdge in edgeModels)
            {
                int eventId = eventEdge.Content.Id;

                // Source == tail, Target == head.
                diagramEdgeModels.Add(new DiagramEdgeModel
                {
                    Id = eventId,
                    SourceId = edgeTailNodeLookup[eventId],
                    TargetId = edgeHeadNodeLookup[eventId],
                    DashStyle = eventEdge.DashStyle.ToGraphDashStyle(),
                    ForegroundColorHexCode = eventEdge.ForegroundColorHexCode,
                    StrokeThickness = eventEdge.StrokeWeight,
                    Label = string.Empty,
                    ShowLabel = false,
                });
            }

            return new DiagramGraphModel
            {
                Nodes = diagramNodeModels,
                Edges = diagramEdgeModels
            };
        }

        private static DiagramNodeModel BuildDiagramNode(ActivityNodeModel activityNode)
        {
            ActivityModel activityModel = activityNode.Content;

            return new DiagramNodeModel
            {
                Id = activityModel.Id,
                Height = c_DiagramNodeHeight,
                Width = c_DiagramNodeWidth,
                FillColorHexCode = s_NodeFillColorHexCode,
                BorderColorHexCode = activityNode.BorderColorHexCode ?? s_NodeBorderColorHexCode,
                BorderDashStyle = activityNode.BorderDashStyle.ToGraphDashStyle(),
                BorderThickness = activityNode.BorderWeight * c_NodeLineThicknessCorrectionFactor,
                Text = BuildNodeLabel(activityModel),
                Name = activityModel.Name,
                Tooltip = BuildNodeTooltip(activityModel),
            };
        }

        private static string BuildNodeLabel(ActivityModel activityModel)
        {
            string labelText = string.Empty;

            if (activityModel.EarliestStartTime is not null
                && activityModel.EarliestFinishTime is not null
                && activityModel.LatestStartTime is not null
                && activityModel.LatestFinishTime is not null)
            {
                string est = $@"{activityModel.EarliestStartTime}";
                string id = $@"{activityModel.Id}";
                string eft = $@"{activityModel.EarliestFinishTime}";

                string lst = $@"{activityModel.LatestStartTime}";
                string duration = $@"{activityModel.Duration}";
                string lft = $@"{activityModel.LatestFinishTime}";

                int leftColumnWidth = Math.Max(est.Length, lst.Length);
                int middleColumnWidth = Math.Max(id.Length, duration.Length);
                int rightColumnWidth = Math.Max(eft.Length, lft.Length);

                leftColumnWidth = Math.Max(leftColumnWidth, rightColumnWidth);
                rightColumnWidth = leftColumnWidth;

                var label = new StringBuilder();

                label.Append($"|{est.PadLeft(leftColumnWidth)}|{id.PadLeft(middleColumnWidth)}|{eft.PadLeft(rightColumnWidth)}|\n");
                label.Append($"+{new string('-', leftColumnWidth)}+{new string('-', middleColumnWidth)}+{new string('-', rightColumnWidth)}+\n");
                label.Append($"|{lst.PadLeft(leftColumnWidth)}|{duration.PadLeft(middleColumnWidth)}|{lft.PadLeft(rightColumnWidth)}|");

                labelText = label.ToString();
            }

            return labelText;
        }

        private static string BuildNodeTooltip(ActivityModel activity)
        {
            static string Format(int? value) => value?.ToString() ?? @"-";

            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(activity.Name))
            {
                builder.AppendLine(activity.Name);
            }
            builder.AppendLine($@"Id: {activity.Id}   Duration: {activity.Duration}");
            builder.AppendLine($@"ES: {Format(activity.EarliestStartTime)}   EF: {Format(activity.EarliestFinishTime)}");
            builder.AppendLine($@"LS: {Format(activity.LatestStartTime)}   LF: {Format(activity.LatestFinishTime)}");
            builder.Append($@"Free slack: {Format(activity.FreeSlack)}   Total slack: {Format(activity.TotalSlack)}");
            return builder.ToString();
        }
    }
}
