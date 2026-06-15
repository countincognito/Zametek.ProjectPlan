using Avalonia.Media;
using System.Text;
using Zametek.Common.ProjectPlan;
using Zametek.Graphs.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    // Builds the library-neutral DiagramGraphModel (what to draw) from the application's
    // ArrowGraphModel (presentation already resolved by GraphPresentationBuilder). This is the
    // Common -> Graphs boundary for the arrow graph: structural validation, the event node label
    // (EF|LF), the activity edge labels (id/duration/slack, single- or multi-line, honouring Show
    // Names), the event and activity tooltips and the Common -> GraphDashStyle mapping all happen
    // here, so the Graphs serializer consumes only its own DiagramGraphModel and carries no
    // dependency on the application's domain models. Validation strings live in Resource.ProjectPlan
    // (the library carries no application-domain strings).
    internal static class ArrowGraphDiagramBuilder
    {
        private const double c_DiagramNodeHeight = 26.0;
        private const double c_DiagramNodeWidth = 62.0;

        // The raw stroke weight is scaled to a comfortable on-diagram thickness.
        private const double c_NodeLineThicknessCorrectionFactor = 1.0;

        private static readonly string s_NodeFillColorHexCode = ColorHelper.ColorToHtmlHexCode(Colors.LightGray);
        private static readonly string s_NodeBorderColorHexCode = ColorHelper.ColorToHtmlHexCode(Colors.Black);

        public static DiagramGraphModel Build(
            ArrowGraphModel arrowGraph,
            bool multiLineEdgeLabels,
            bool viewNames)
        {
            ArgumentNullException.ThrowIfNull(arrowGraph);

            IList<EventNodeModel> nodeModels = arrowGraph.Nodes;

            var edgeHeadNodeLookup = new Dictionary<int, int>();
            var edgeTailNodeLookup = new Dictionary<int, int>();
            var drawingGraphNodeIds = new List<int>();

            foreach (EventNodeModel node in nodeModels)
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
            IList<ActivityEdgeModel> edgeModels = arrowGraph.Edges;
            IEnumerable<int> edgeIds = edgeModels.Select(x => x.Content.Id);

            if (!edgeIds.OrderBy(x => x).SequenceEqual(edgeHeadNodeLookup.Keys.OrderBy(x => x)))
            {
                throw new ArgumentException(Resource.ProjectPlan.Messages.Message_MismatchedEdgeIdsForHeadNodesInArrowGraph);
            }
            if (!edgeIds.OrderBy(x => x).SequenceEqual(edgeTailNodeLookup.Keys.OrderBy(x => x)))
            {
                throw new ArgumentException(Resource.ProjectPlan.Messages.Message_MismatchedEdgeIdsForTailNodesInArrowGraph);
            }

            // Check all events are used.
            IEnumerable<int> edgeNodeLookupIds = edgeHeadNodeLookup.Values.Union(edgeTailNodeLookup.Values);

            if (!drawingGraphNodeIds.OrderBy(x => x).SequenceEqual(edgeNodeLookupIds.OrderBy(x => x)))
            {
                throw new ArgumentException(Resource.ProjectPlan.Messages.Message_MismatchedNodeIdsAssociatedWithEdgesInArrowGraph);
            }

            // Check Start and End nodes.
            if (nodeModels.Count(x => x.NodeType == Maths.Graphs.NodeType.Start) > 1)
            {
                throw new ArgumentException(Resource.ProjectPlan.Messages.Message_ArrowGraphDataContainMultipleStartNodes);
            }
            if (nodeModels.Count(x => x.NodeType == Maths.Graphs.NodeType.End) > 1)
            {
                throw new ArgumentException(Resource.ProjectPlan.Messages.Message_ArrowGraphDataContainMultipleEndNodes);
            }

            List<DiagramNodeModel> diagramNodeModels = [.. nodeModels.Select(BuildDiagramNode)];
            List<DiagramEdgeModel> diagramEdgeModels = [];

            foreach (ActivityEdgeModel activityEdge in edgeModels)
            {
                ActivityModel activityModel = activityEdge.Content;
                int activityId = activityModel.Id;

                (bool showLabel, string labelText) = multiLineEdgeLabels
                    ? BuildMultiLineEdgeLabel(activityModel, activityEdge.IsDummy, activityEdge.IsCritical, viewNames)
                    : BuildSingleLineEdgeLabel(activityModel, activityEdge.IsDummy, activityEdge.IsCritical, viewNames);

                // Source == tail, Target == head.
                diagramEdgeModels.Add(new DiagramEdgeModel
                {
                    Id = activityId,
                    Name = activityModel.Name,
                    SourceId = edgeTailNodeLookup[activityId],
                    TargetId = edgeHeadNodeLookup[activityId],
                    DashStyle = activityEdge.DashStyle.ToGraphDashStyle(),
                    ForegroundColorHexCode = activityEdge.ForegroundColorHexCode,
                    StrokeThickness = activityEdge.StrokeWeight,
                    Label = labelText,
                    ShowLabel = showLabel,
                    Tooltip = BuildEdgeTooltip(activityModel),
                });
            }

            return new DiagramGraphModel
            {
                Nodes = diagramNodeModels,
                Edges = diagramEdgeModels
            };
        }

        private static DiagramNodeModel BuildDiagramNode(EventNodeModel eventNode)
        {
            EventModel eventModel = eventNode.Content;
            string text = BuildNodeLabel(eventModel);

            return new DiagramNodeModel
            {
                Id = eventModel.Id,
                Height = c_DiagramNodeHeight,
                Width = c_DiagramNodeWidth,
                FillColorHexCode = s_NodeFillColorHexCode,
                BorderColorHexCode = eventNode.BorderColorHexCode ?? s_NodeBorderColorHexCode,
                BorderDashStyle = eventNode.BorderDashStyle.ToGraphDashStyle(),
                BorderThickness = eventNode.BorderWeight * c_NodeLineThicknessCorrectionFactor,
                Text = text,
                Name = text,
                Tooltip = BuildNodeTooltip(eventModel),
            };
        }

        private static string BuildNodeLabel(EventModel eventModel)
        {
            string labelText = string.Empty;

            if (eventModel.EarliestFinishTime is not null
                && eventModel.LatestFinishTime is not null)
            {
                labelText = $@"{eventModel.EarliestFinishTime}|{eventModel.LatestFinishTime}";
            }

            return labelText;
        }

        private static string BuildNodeTooltip(EventModel eventModel)
        {
            static string Format(int? value) => value?.ToString() ?? @"-";

            return $@"EF: {Format(eventModel.EarliestFinishTime)}   LF: {Format(eventModel.LatestFinishTime)}";
        }

        // An activity edge and a vertex node both represent an activity, so both surface the same
        // id/duration/times/slack information.
        private static string BuildEdgeTooltip(ActivityModel activity)
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

        private static (bool isVisible, string labelText) BuildSingleLineEdgeLabel(ActivityModel activityModel, bool isDummy, bool isCritical, bool viewNames)
        {
            var labelText = new StringBuilder();
            bool isVisible = false;

            if (isDummy)
            {
                if (!activityModel.CanBeRemoved)
                {
                    labelText.Append(@$"{activityModel.Id}");
                    if (viewNames)
                    {
                        labelText.Append(@$" {activityModel.Name}");
                    }
                    if (!isCritical)
                    {
                        labelText.Append(@$" [{activityModel.FreeSlack}|{activityModel.TotalSlack}]");
                    }
                    isVisible = true;
                }
                else
                {
                    if (!isCritical)
                    {
                        labelText.Append(@$"[{activityModel.FreeSlack}|{activityModel.TotalSlack}]");
                        isVisible = true;
                    }
                }
            }
            else
            {
                labelText.Append(@$"{activityModel.Id}");
                if (viewNames)
                {
                    labelText.Append(@$" {activityModel.Name}");
                }
                labelText.Append(@$" ({activityModel.Duration})");
                if (!isCritical)
                {
                    labelText.Append(@$" [{activityModel.FreeSlack}|{activityModel.TotalSlack}]");
                }
                isVisible = true;
            }
            return (isVisible, labelText.ToString());
        }

        private static (bool isVisible, string labelText) BuildMultiLineEdgeLabel(ActivityModel activityModel, bool isDummy, bool isCritical, bool viewNames)
        {
            var labelText = new StringBuilder();
            bool isVisible = false;

            if (isDummy)
            {
                if (!activityModel.CanBeRemoved)
                {
                    labelText.AppendFormat($@"{activityModel.Id}");
                    if (viewNames)
                    {
                        labelText.AppendFormat(@$" {activityModel.Name}");
                    }
                    if (!isCritical)
                    {
                        labelText.AppendLine();
                        labelText.AppendFormat($@"{activityModel.FreeSlack}|{activityModel.TotalSlack}");
                    }
                    isVisible = true;
                }
                else
                {
                    if (!isCritical)
                    {
                        labelText.AppendFormat($@"{activityModel.FreeSlack}|{activityModel.TotalSlack}");
                        isVisible = true;
                    }
                }
            }
            else
            {
                labelText.AppendFormat($@"{activityModel.Id}");
                if (viewNames)
                {
                    labelText.AppendFormat(@$" {activityModel.Name}");
                }
                labelText.AppendFormat($@" ({activityModel.Duration})");
                if (!isCritical)
                {
                    labelText.AppendLine();
                    labelText.AppendFormat($@"{activityModel.FreeSlack}|{activityModel.TotalSlack}");
                }
                isVisible = true;
            }
            return (isVisible, labelText.ToString());
        }
    }
}
