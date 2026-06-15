using Avalonia.Media;
using System.Text;
using System.Xml.Serialization;
using Zametek.Common.ProjectPlan;
using Zametek.Utility;

namespace Zametek.Graphs.ProjectPlan
{
    public class ArrowGraphSerializer
        : IArrowGraphSerializer
    {
        #region Fields

        private static readonly Dictionary<EdgeDashStyle, Microsoft.Msagl.Drawing.Style> s_EdgeDashMsaglLookup =
             new()
             {
                {EdgeDashStyle.Normal, Microsoft.Msagl.Drawing.Style.Solid},
                {EdgeDashStyle.Dashed, Microsoft.Msagl.Drawing.Style.Dashed}
             };
        private static readonly Dictionary<NodeBorderDashStyle, Microsoft.Msagl.Drawing.Style> s_NodeBorderDashMsaglLookup =
             new()
             {
                {NodeBorderDashStyle.Normal, Microsoft.Msagl.Drawing.Style.Solid},
                {NodeBorderDashStyle.Dashed, Microsoft.Msagl.Drawing.Style.Dashed}
             };

        private static readonly double s_SvgNodeWidth = 40.0;
        private static readonly double s_SvgNodeHeight = 34.0;
        private static readonly double s_SvgNodeLabelWidth = 34.0;
        private static readonly double s_SvgNodeLabelLines = 1.0;
        private static readonly double s_SvgRadiusInXDirection = 3.0;
        private static readonly double s_SvgRadiusInYDirection = 2.0;
        private static readonly double s_SvgNodeLineThicknessCorrectionFactor = 1.0;

        private static readonly double s_SvgEdgeLabelFontSize = 12.0;
        private static readonly double s_SvgEdgeLabelHeight = 12.0;
        private static readonly Microsoft.Msagl.Drawing.FontStyle s_SvgNodeFontStyle = Microsoft.Msagl.Drawing.FontStyle.Regular;

        private static readonly double s_DiagramNodeModelHeight = 26.0;
        private static readonly double s_DiagramNodeModelWidth = 62.0;

        // These need to be worked out through trial and error
        // whenever s_SvgNodeLabelWidth is changed.
        private static readonly double s_SvgConsolasLabelWidthCorrectionFactor = s_SvgNodeLabelLines * s_SvgNodeLabelWidth / 14;
        private static readonly double s_SvgConsolasLabelHeightCorrectionFactor = 0.7;

        private static readonly Color s_NodeFillColor = Colors.LightGray;
        private static readonly Color s_NodeBorderColor = Colors.Black;

        private const double c_PxPerInch = 96;
        private const double c_PtPerInch = 72;
        private const string c_FontName = @"Consolas";

        private readonly IMsaglSvgRenderer m_MsaglSvgRenderer;

        #endregion

        #region Ctors

        public ArrowGraphSerializer(IMsaglSvgRenderer msaglSvgRenderer)
        {
            ArgumentNullException.ThrowIfNull(msaglSvgRenderer);
            m_MsaglSvgRenderer = msaglSvgRenderer;
        }

        #endregion

        #region Private Methods

        private static string BuildNodeLabel(EventModel eventModel)
        {
            ArgumentNullException.ThrowIfNull(eventModel);
            string labelText = string.Empty;

            if (eventModel.EarliestFinishTime is not null
                && eventModel.LatestFinishTime is not null)
            {
                labelText = $@"{eventModel.EarliestFinishTime}|{eventModel.LatestFinishTime}";
            }

            return labelText;
        }

        private static DiagramNodeModel BuildDiagramNode(EventNodeModel eventNode)
        {
            ArgumentNullException.ThrowIfNull(eventNode);
            EventModel eventModel = eventNode.Content;

            string text = BuildNodeLabel(eventModel);

            return new DiagramNodeModel
            {
                Id = eventModel.Id,
                Height = s_DiagramNodeModelHeight,
                Width = s_DiagramNodeModelWidth,
                FillColorHexCode = ColorHelper.ColorToHtmlHexCode(s_NodeFillColor),
                BorderColorHexCode = eventNode.BorderColorHexCode ?? ColorHelper.ColorToHtmlHexCode(s_NodeBorderColor),
                BorderDashStyle = eventNode.BorderDashStyle,
                BorderThickness = eventNode.BorderWeight * s_SvgNodeLineThicknessCorrectionFactor,
                Text = text,
                Name = text,
            };
        }

        private static (bool isVisible, string labelText) BuildSingleLineEdgeLabel(ActivityModel activityModel, bool isDummy, bool isCritical, bool viewNames)
        {
            ArgumentNullException.ThrowIfNull(activityModel);
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
            ArgumentNullException.ThrowIfNull(activityModel);
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

        private static DiagramGraphModel BuildGraphDiagram(
            ArrowGraphModel arrowGraphModel,
            bool multiLineEdgeLabels = false,
            bool viewNames = false)
        {
            ArgumentNullException.ThrowIfNull(arrowGraphModel);
            // Perform validity check.
            IList<EventNodeModel> nodeModels = arrowGraphModel.Nodes;
            IDictionary<int, EventNodeModel> nodeModelLookup = nodeModels.ToDictionary(x => x.Content.Id);

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
            IList<ActivityEdgeModel> edgeModels = arrowGraphModel.Edges;
            IDictionary<int, ActivityEdgeModel> edgeModelLookup = edgeModels.ToDictionary(x => x.Content.Id);
            IEnumerable<int> edgeIds = edgeModelLookup.Keys;

            if (!edgeIds.OrderBy(x => x).SequenceEqual(edgeHeadNodeLookup.Keys.OrderBy(x => x)))
            {
                throw new ArgumentException(Messages.Message_MismatchedEdgeIdsForHeadNodesInArrowGraph);
            }
            if (!edgeIds.OrderBy(x => x).SequenceEqual(edgeTailNodeLookup.Keys.OrderBy(x => x)))
            {
                throw new ArgumentException(Messages.Message_MismatchedEdgeIdsForTailNodesInArrowGraph);
            }

            // Check all events are used.
            IEnumerable<int> edgeNodeLookupIds = edgeHeadNodeLookup.Values.Union(edgeTailNodeLookup.Values);

            if (!drawingGraphNodeIds.OrderBy(x => x).SequenceEqual(edgeNodeLookupIds.OrderBy(x => x)))
            {
                throw new ArgumentException(Messages.Message_MismatchedNodeIdsAssociatedWithEdgesInArrowGraph);
            }

            // Check Start and End nodes.
            IEnumerable<EventNodeModel> startNodes = nodeModels.Where(x => x.NodeType == Maths.Graphs.NodeType.Start);
            if (startNodes.Count() > 1)
            {
                throw new ArgumentException(Messages.Message_ArrowGraphDataContainMultipleStartNodes);
            }

            IEnumerable<EventNodeModel> endNodes = nodeModels.Where(x => x.NodeType == Maths.Graphs.NodeType.End);
            if (endNodes.Count() > 1)
            {
                throw new ArgumentException(Messages.Message_ArrowGraphDataContainMultipleEndNodes);
            }

            // Fill the graph. Presentation (border/edge colour, dash, weight) is resolved by the
            // application beforehand and read straight off the models here; only the labels (which
            // depend on the per-call viewNames/multiLine options) are still built here.
            List<DiagramNodeModel> diagramNodeModels = nodeModels.Select(BuildDiagramNode).ToList();
            List<DiagramEdgeModel> diagramEdgeModels = [];

            foreach (ActivityEdgeModel activityEdge in edgeModels)
            {
                ActivityModel activityModel = activityEdge.Content;
                int activityId = activityModel.Id;
                bool showLabel;
                string labelText;

                if (multiLineEdgeLabels)
                {
                    (showLabel, labelText) = BuildMultiLineEdgeLabel(activityModel, activityEdge.IsDummy, activityEdge.IsCritical, viewNames);
                }
                else
                {
                    (showLabel, labelText) = BuildSingleLineEdgeLabel(activityModel, activityEdge.IsDummy, activityEdge.IsCritical, viewNames);
                }

                // Source == tail
                // Target == head
                var diagramEdgeModel = new DiagramEdgeModel
                {
                    Id = activityId,
                    Name = activityModel.Name,
                    SourceId = edgeTailNodeLookup[activityId],
                    TargetId = edgeHeadNodeLookup[activityId],
                    DashStyle = activityEdge.DashStyle,
                    ForegroundColorHexCode = activityEdge.ForegroundColorHexCode,
                    StrokeThickness = activityEdge.StrokeWeight,
                    Label = labelText,
                    ShowLabel = showLabel,
                };

                diagramEdgeModels.Add(diagramEdgeModel);
            }

            return new DiagramGraphModel
            {
                Nodes = diagramNodeModels,
                Edges = diagramEdgeModels
            };
        }

        private static Microsoft.Msagl.Drawing.Color? HtmlHexCodeToMsaglColor(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            ColorFormatModel colorFormat = ColorHelper.HtmlHexCodeToColorFormat(input);

            return new Microsoft.Msagl.Drawing.Color
            {
                A = colorFormat.A,
                R = colorFormat.R,
                G = colorFormat.G,
                B = colorFormat.B
            };
        }

        private static Microsoft.Msagl.Drawing.Color EdgeFontColor(BaseTheme baseTheme)
        {
            if (baseTheme == BaseTheme.Light)
            {
                return Microsoft.Msagl.Drawing.Color.Black;
            }
            if (baseTheme == BaseTheme.Dark)
            {
                return Microsoft.Msagl.Drawing.Color.White;
            }
            return Microsoft.Msagl.Drawing.Color.Black;
        }

        #endregion

        private (Microsoft.Msagl.Drawing.Graph DrawingGraph, DiagramGraphModel Diagram) BuildAndLayoutDrawingGraph(
            ArrowGraphModel arrowGraph,
            BaseTheme baseTheme,
            bool viewNames)
        {
            ArgumentNullException.ThrowIfNull(arrowGraph);
            DiagramGraphModel diagramGraph = BuildGraphDiagram(arrowGraph, viewNames: viewNames);

            // Fill the graph.
            var drawingGraph = new Microsoft.Msagl.Drawing.Graph();

            foreach (DiagramNodeModel diagramNode in diagramGraph.Nodes)
            {
                var drawingGraphNode = new Microsoft.Msagl.Drawing.Node($@"{diagramNode.Id}");
                drawingGraph.AddNode(drawingGraphNode);
            }

            Dictionary<string, Microsoft.Msagl.Drawing.Node> drawingNodeLookup = drawingGraph.Nodes.ToDictionary(x => x.Id);

            foreach (DiagramEdgeModel diagramEdge in diagramGraph.Edges)
            {
                var edge = new Microsoft.Msagl.Drawing.Edge(
                    drawingNodeLookup[$@"{diagramEdge.SourceId}"],
                    drawingNodeLookup[$@"{diagramEdge.TargetId}"],
                    Microsoft.Msagl.Drawing.ConnectionToGraph.Connected);

                edge.Attr.ClearStyles();
                edge.Attr.AddStyle(s_EdgeDashMsaglLookup[diagramEdge.DashStyle]);
                edge.Attr.Color = HtmlHexCodeToMsaglColor(diagramEdge.ForegroundColorHexCode) ?? Microsoft.Msagl.Drawing.Color.Black;
                edge.Attr.LineWidth = diagramEdge.StrokeThickness;
                edge.LabelText = diagramEdge.Label;
                edge.Label.IsVisible = diagramEdge.ShowLabel;
                edge.Label.FontColor = EdgeFontColor(baseTheme);

                drawingGraph.AddPrecalculatedEdge(edge);
            }

            drawingGraph.LayoutAlgorithmSettings = drawingGraph.CreateLayoutSettings();

            drawingGraph.LayoutAlgorithmSettings.EdgeRoutingSettings.UseObstacleRectangles = true;
            drawingGraph.LayoutAlgorithmSettings.EdgeRoutingSettings.EdgeRoutingMode = Microsoft.Msagl.Core.Routing.EdgeRoutingMode.SugiyamaSplines;

            drawingGraph.Attr.LayerDirection = Microsoft.Msagl.Drawing.LayerDirection.LR;

            // Draw the graph.
            drawingGraph.CreateGeometryGraph();

            Dictionary<string, DiagramNodeModel> diagramNodeLookup = diagramGraph.Nodes.ToDictionary(x => $@"{x.Id}");

            // Fill the nodes.
            foreach (Microsoft.Msagl.Drawing.Node drawingGraphNode in drawingGraph.Nodes)
            {
                DiagramNodeModel diagramNode = diagramNodeLookup[drawingGraphNode.Id];

                // Make sure this is set before the label is updated.
                drawingGraphNode.LabelText = diagramNode.Text ?? string.Empty;

                // Calculate the correct label font size (Pts) and the label height (Pxs)
                // based off of the pt->px conversion, with Consolas correction factors.
                double nodeLabelFontSize = s_SvgConsolasLabelWidthCorrectionFactor * s_SvgNodeLabelWidth * c_PtPerInch / (drawingGraphNode.LabelText.Length * c_PxPerInch);
                double nodeLabelHeight = s_SvgConsolasLabelHeightCorrectionFactor * nodeLabelFontSize * c_PxPerInch / c_PtPerInch;
                double nodeHeight = s_SvgNodeHeight;

                drawingGraphNode.GeometryNode.BoundaryCurve =
                    Microsoft.Msagl.Core.Geometry.Curves.CurveFactory.CreateRectangleWithRoundedCorners(
                        s_SvgNodeWidth,
                        nodeHeight,
                        s_SvgRadiusInXDirection,
                        s_SvgRadiusInYDirection,
                        new Microsoft.Msagl.Core.Geometry.Point(0, 0));

                drawingGraphNode.Label.Height = nodeLabelHeight;
                drawingGraphNode.Label.Width = s_SvgNodeLabelWidth;
                drawingGraphNode.Label.FontSize = nodeLabelFontSize;
                drawingGraphNode.Label.FontStyle = s_SvgNodeFontStyle;

                drawingGraphNode.Label.FontName = c_FontName;
                drawingGraphNode.Attr.AddStyle(s_NodeBorderDashMsaglLookup[diagramNode.BorderDashStyle]);
                drawingGraphNode.Attr.FillColor = HtmlHexCodeToMsaglColor(diagramNode.FillColorHexCode) ?? Microsoft.Msagl.Drawing.Color.LightGray;
                drawingGraphNode.Attr.Color = HtmlHexCodeToMsaglColor(diagramNode.BorderColorHexCode) ?? Microsoft.Msagl.Drawing.Color.Black;
                drawingGraphNode.Attr.LineWidth = diagramNode.BorderThickness;
            }

            // Initialise geometry labels as well.
            foreach (Microsoft.Msagl.Drawing.Edge drawingGraphEdge in drawingGraph.Edges)
            {
                double edgeLabelWidth = drawingGraphEdge.LabelText.Length * s_SvgEdgeLabelFontSize * (c_PxPerInch / c_PtPerInch) / s_SvgConsolasLabelWidthCorrectionFactor;

                drawingGraphEdge.Label.FontName = c_FontName;
                drawingGraphEdge.Label.FontSize = s_SvgEdgeLabelFontSize;
                drawingGraphEdge.Label.GeometryLabel.Width = edgeLabelWidth;
                drawingGraphEdge.Label.GeometryLabel.Height = s_SvgEdgeLabelHeight;
                drawingGraphEdge.Label.GeometryLabel.Center = new Microsoft.Msagl.Core.Geometry.Point(0, 0);
                drawingGraphEdge.Label.GeometryLabel.PlacementResult = Microsoft.Msagl.Core.Layout.LabelPlacementResult.OverlapsNothing;
            }

            Microsoft.Msagl.Miscellaneous.LayoutHelpers.CalculateLayout(drawingGraph.GeometryGraph, drawingGraph.LayoutAlgorithmSettings, null);

            return (drawingGraph, diagramGraph);
        }

        public byte[] BuildArrowGraphSvgData(
            ArrowGraphModel arrowGraph,
            BaseTheme baseTheme,
            bool viewNames)
        {
            (Microsoft.Msagl.Drawing.Graph drawingGraph, _) =
                BuildAndLayoutDrawingGraph(arrowGraph, baseTheme, viewNames);

            return m_MsaglSvgRenderer.RenderToSvg(drawingGraph, baseTheme);
        }

        // Produce on-screen geometry for the interactive arrow-graph control, reusing the same
        // MSAGL layout that drives the SVG. MSAGL works in a Y-up coordinate space; the SVG writer
        // flips this internally, so here we flip it ourselves and scale uniformly so the small
        // layout boxes become a comfortable interactive size while preserving relative positions.
        private const double c_InteractiveLayoutScale = 2.5;

        public GraphLayoutModel BuildArrowGraphLayout(
            ArrowGraphModel arrowGraph,
            BaseTheme baseTheme,
            bool viewNames)
        {
            (Microsoft.Msagl.Drawing.Graph drawingGraph, DiagramGraphModel diagramGraph) =
                BuildAndLayoutDrawingGraph(arrowGraph, baseTheme, viewNames);

            return ExtractLayout(drawingGraph, diagramGraph, arrowGraph);
        }

        private static GraphLayoutModel ExtractLayout(
            Microsoft.Msagl.Drawing.Graph drawingGraph,
            DiagramGraphModel diagramGraph,
            ArrowGraphModel arrowGraph)
        {
            Microsoft.Msagl.Core.Geometry.Rectangle boundingBox = drawingGraph.GeometryGraph.BoundingBox;
            double graphLeft = boundingBox.Left;
            double graphTop = boundingBox.Top; // Largest Y in MSAGL's Y-up space.

            Dictionary<int, DiagramNodeModel> diagramNodeLookup = diagramGraph.Nodes.ToDictionary(x => x.Id);
            Dictionary<int, EventModel> eventLookup = arrowGraph.Nodes
                .Select(x => x.Content)
                .Where(x => x is not null)
                .ToDictionary(x => x.Id);

            var nodes = new List<GraphNodeLayoutModel>();

            foreach (Microsoft.Msagl.Drawing.Node drawingNode in drawingGraph.Nodes)
            {
                if (!int.TryParse(drawingNode.Id, out int id)
                    || !diagramNodeLookup.TryGetValue(id, out DiagramNodeModel? diagramNode))
                {
                    continue;
                }

                Microsoft.Msagl.Core.Layout.Node? geometryNode = drawingNode.GeometryNode;
                if (geometryNode is null)
                {
                    continue;
                }

                double width = geometryNode.Width;
                double height = geometryNode.Height;

                // MSAGL centre -> top-left, with Y flipped, then scaled uniformly.
                double centreX = geometryNode.Center.X - graphLeft;
                double centreY = graphTop - geometryNode.Center.Y;

                eventLookup.TryGetValue(id, out EventModel? eventModel);

                nodes.Add(new GraphNodeLayoutModel
                {
                    Id = id,
                    X = (centreX - (width / 2.0)) * c_InteractiveLayoutScale,
                    Y = (centreY - (height / 2.0)) * c_InteractiveLayoutScale,
                    Width = width * c_InteractiveLayoutScale,
                    Height = height * c_InteractiveLayoutScale,
                    Label = diagramNode.Text ?? string.Empty,
                    Name = diagramNode.Name,
                    Tooltip = BuildNodeTooltip(eventModel, diagramNode),
                    FillColorHexCode = diagramNode.FillColorHexCode,
                    BorderColorHexCode = diagramNode.BorderColorHexCode,
                    BorderThickness = diagramNode.BorderThickness,
                    IsDashed = diagramNode.BorderDashStyle == NodeBorderDashStyle.Dashed,
                });
            }

            // Activity edges carry the same rich tooltip the vertex graph puts on its activity
            // nodes (both represent activities). The diagram edge id is the activity id.
            Dictionary<int, ActivityModel> activityLookup = arrowGraph.Edges
                .Select(x => x.Content)
                .Where(x => x is not null)
                .ToDictionary(x => x.Id);

            List<GraphEdgeLayoutModel> edges = diagramGraph.Edges
                .Select(x => new GraphEdgeLayoutModel
                {
                    Id = x.Id,
                    SourceId = x.SourceId,
                    TargetId = x.TargetId,
                    StrokeThickness = x.StrokeThickness,
                    IsDashed = x.DashStyle == EdgeDashStyle.Dashed,
                    ForegroundColorHexCode = x.ForegroundColorHexCode,
                    Label = x.Label,
                    ShowLabel = x.ShowLabel,
                    Tooltip = BuildEdgeTooltip(activityLookup.GetValueOrDefault(x.Id), x),
                })
                .ToList();

            return new GraphLayoutModel
            {
                Width = boundingBox.Width * c_InteractiveLayoutScale,
                Height = boundingBox.Height * c_InteractiveLayoutScale,
                Nodes = nodes,
                Edges = edges,
            };
        }

        private static string BuildNodeTooltip(EventModel? eventModel, DiagramNodeModel diagramNode)
        {
            if (eventModel is null)
            {
                return diagramNode.Name ?? diagramNode.Text ?? string.Empty;
            }

            static string Format(int? value) => value?.ToString() ?? @"-";

            return $@"EF: {Format(eventModel.EarliestFinishTime)}   LF: {Format(eventModel.LatestFinishTime)}";
        }

        // Mirrors the vertex graph's activity-node tooltip: an activity edge and a vertex node both
        // represent an activity, so both surface the same id/duration/times/slack information.
        private static string BuildEdgeTooltip(ActivityModel? activity, DiagramEdgeModel diagramEdge)
        {
            if (activity is null)
            {
                return diagramEdge.Name ?? diagramEdge.Label ?? string.Empty;
            }

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

        public byte[] BuildArrowGraphMLData(
            ArrowGraphModel arrowGraph,
            bool viewNames)
        {
            DiagramGraphModel diagramGraph = BuildGraphDiagram(arrowGraph, multiLineEdgeLabels: true, viewNames: viewNames);
            graphml graphML = GraphMLBuilder.ToGraphML(diagramGraph);
            using var ms = new MemoryStream();
            var xmlSerializer = new XmlSerializer(typeof(graphml));
            xmlSerializer.Serialize(ms, graphML);
            ms.Position = 0;
            using var sr = new StreamReader(ms);
            string content = sr.ReadToEnd();
            return content.StringToByteArray();
        }

        public byte[] BuildArrowGraphVizData(
            ArrowGraphModel arrowGraph,
            bool viewNames)
        {
            DiagramGraphModel diagramGraph = BuildGraphDiagram(arrowGraph, multiLineEdgeLabels: true, viewNames: viewNames);
            string graphviz = GraphVizBuilder.ToGraphViz(diagramGraph);
            return graphviz.StringToByteArray();
        }
    }
}
