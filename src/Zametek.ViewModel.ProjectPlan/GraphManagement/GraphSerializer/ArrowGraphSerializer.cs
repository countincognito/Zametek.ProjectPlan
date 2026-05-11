using Avalonia.Media;
using System.Text;
using System.Xml.Serialization;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
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

        private static readonly double s_SvgNodeWidth = 48.0;
        private static readonly double s_SvgNodeHeight = 38.0;
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

        private static readonly Color s_NodeFillColor = Color.FromRgb(0x1E, 0x29, 0x3B);   // dark slate
        private static readonly Color s_NodeBorderColor = Color.FromRgb(0x3B, 0x82, 0xF6); // blue
        private static readonly Microsoft.Msagl.Drawing.Color s_NodeLabelColor = new(0xF1, 0xF5, 0xF9); // near-white

        private const double c_PxPerInch = 96;
        private const double c_PtPerInch = 72;
        private const string c_FontName = @"Inter";

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

        private static DiagramNodeModel BuildDiagramNode(
            EventNodeModel eventNode,
            GraphNodeBorderFormatLookup nodeFormatLookup)
        {
            ArgumentNullException.ThrowIfNull(eventNode);
            ArgumentNullException.ThrowIfNull(nodeFormatLookup);
            EventModel eventModel = eventNode.Content;

            string text = BuildNodeLabel(eventModel);

            return new DiagramNodeModel
            {
                Id = eventModel.Id,
                Height = s_DiagramNodeModelHeight,
                Width = s_DiagramNodeModelWidth,
                FillColorHexCode = ColorHelper.ColorToHtmlHexCode(s_NodeFillColor),
                BorderColorHexCode = ColorHelper.ColorToHtmlHexCode(s_NodeBorderColor),
                BorderDashStyle = nodeFormatLookup.FindGraphNodeBorderDashStyle(false, false),
                BorderThickness = nodeFormatLookup.FindBorderThickness(false, false) * s_SvgNodeLineThicknessCorrectionFactor,
                Text = text,
                Name = text,
            };
        }

        private static (bool isVisible, string labelText) BuildSingleLineEdgeLabel(ActivityModel activityModel, bool viewNames)
        {
            ArgumentNullException.ThrowIfNull(activityModel);
            var labelText = new StringBuilder();
            bool isVisible = false;

            if (activityModel.IsDummy())
            {
                if (!activityModel.CanBeRemoved)
                {
                    labelText.Append(@$"{activityModel.Id}");
                    if (viewNames)
                    {
                        labelText.Append(@$" {activityModel.Name}");
                    }
                    if (!activityModel.IsCritical())
                    {
                        labelText.Append(@$" [{activityModel.FreeSlack}|{activityModel.TotalSlack}]");
                    }
                    isVisible = true;
                }
                else
                {
                    if (!activityModel.IsCritical())
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
                if (!activityModel.IsCritical())
                {
                    labelText.Append(@$" [{activityModel.FreeSlack}|{activityModel.TotalSlack}]");
                }
                isVisible = true;
            }
            return (isVisible, labelText.ToString());
        }

        private static (bool isVisible, string labelText) BuildMultiLineEdgeLabel(ActivityModel activityModel, bool viewNames)
        {
            ArgumentNullException.ThrowIfNull(activityModel);
            var labelText = new StringBuilder();
            bool isVisible = false;

            if (activityModel.IsDummy())
            {
                if (!activityModel.CanBeRemoved)
                {
                    labelText.AppendFormat($@"{activityModel.Id}");
                    if (viewNames)
                    {
                        labelText.AppendFormat(@$" {activityModel.Name}");
                    }
                    if (!activityModel.IsCritical())
                    {
                        labelText.AppendLine();
                        labelText.AppendFormat($@"{activityModel.FreeSlack}|{activityModel.TotalSlack}");
                    }
                    isVisible = true;
                }
                else
                {
                    if (!activityModel.IsCritical())
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
                if (!activityModel.IsCritical())
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
            GraphSettingsModel graphSettingsModel,
            bool multiLineEdgeLabels = false,
            bool viewNames = false)
        {
            ArgumentNullException.ThrowIfNull(arrowGraphModel);
            ArgumentNullException.ThrowIfNull(graphSettingsModel);
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
            IEnumerable<EventNodeModel> startNodes = nodeModels.Where(x => x.NodeType == Maths.Graphs.NodeType.Start);
            if (startNodes.Count() > 1)
            {
                throw new ArgumentException(Resource.ProjectPlan.Messages.Message_ArrowGraphDataContainMultipleStartNodes);
            }

            IEnumerable<EventNodeModel> endNodes = nodeModels.Where(x => x.NodeType == Maths.Graphs.NodeType.End);
            if (endNodes.Count() > 1)
            {
                throw new ArgumentException(Resource.ProjectPlan.Messages.Message_ArrowGraphDataContainMultipleEndNodes);
            }

            var nodeFormatLookup = new GraphNodeBorderFormatLookup(graphSettingsModel.NodeTypeFormats);
            var edgeFormatLookup = new GraphEdgeFormatLookup(graphSettingsModel.EdgeTypeFormats);
            var colorFormatLookup = new SlackColorFormatLookup(graphSettingsModel.ActivitySeverities);

            // Fill the graph.
            List<DiagramNodeModel> diagramNodeModels = nodeModels.Select(x => BuildDiagramNode(x, nodeFormatLookup)).ToList();
            List<DiagramEdgeModel> diagramEdgeModels = [];

            foreach (ActivityEdgeModel activityEdge in edgeModels)
            {
                ActivityModel activityModel = activityEdge.Content;
                int activityId = activityModel.Id;
                bool isCritical = activityModel.IsCritical();
                bool isDummy = activityModel.IsDummy();
                bool showLabel = false;
                string labelText = string.Empty;

                if (multiLineEdgeLabels)
                {
                    (showLabel, labelText) = BuildMultiLineEdgeLabel(activityModel, viewNames);
                }
                else
                {
                    (showLabel, labelText) = BuildSingleLineEdgeLabel(activityModel, viewNames);
                }

                // Source == tail
                // Target == head
                var diagramEdgeModel = new DiagramEdgeModel
                {
                    Id = activityId,
                    Name = activityModel.Name,
                    SourceId = edgeTailNodeLookup[activityId],
                    TargetId = edgeHeadNodeLookup[activityId],
                    DashStyle = edgeFormatLookup.FindGraphEdgeDashStyle(isCritical, isDummy),
                    ForegroundColorHexCode = ColorHelper.ColorFormatToHtmlHexCode(colorFormatLookup.FindSlackColorFormat(activityModel.TotalSlack)),
                    StrokeThickness = edgeFormatLookup.FindStrokeThickness(isCritical, isDummy),
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
            // Muted slate — readable on both light and dark SVG backgrounds.
            return new Microsoft.Msagl.Drawing.Color(0x94, 0xA3, 0xB8);
        }

        #endregion

        // Margin used by SvgGraphWriter when computing positions in SVG space.
        private const double c_SvgMargin = 1.0;

        private static IReadOnlyList<GraphEdgeHitRect> ComputeEdgeHitRects(
            Microsoft.Msagl.Drawing.Graph drawingGraph,
            Dictionary<Microsoft.Msagl.Drawing.Edge, int> edgeToActivityId)
        {
            var hitRects = new List<GraphEdgeHitRect>();

            Microsoft.Msagl.Core.Geometry.Rectangle bb = drawingGraph.GeometryGraph.BoundingBox;

            foreach (Microsoft.Msagl.Drawing.Edge drawingEdge in drawingGraph.Edges)
            {
                if (!edgeToActivityId.TryGetValue(drawingEdge, out int activityId))
                {
                    continue;
                }

                if (drawingEdge.Label is null || !drawingEdge.Label.IsVisible)
                {
                    continue;
                }

                Microsoft.Msagl.Core.Layout.Label? geomLabel = drawingEdge.Label.GeometryLabel;
                if (geomLabel is null)
                {
                    continue;
                }

                double w = geomLabel.Width;
                double h = geomLabel.Height;
                double cx = geomLabel.Center.X - bb.Left + c_SvgMargin;
                double cy = bb.Top - geomLabel.Center.Y + c_SvgMargin;

                hitRects.Add(new GraphEdgeHitRect(
                    activityId,
                    LabelX: cx - w / 2.0,
                    LabelY: cy - h / 2.0,
                    LabelWidth: w,
                    LabelHeight: h));
            }

            return hitRects.AsReadOnly();
        }

        public (byte[] SvgData, IReadOnlyList<GraphEdgeHitRect> EdgeHitRects) BuildArrowGraphSvgData(
            ArrowGraphModel arrowGraph,
            GraphSettingsModel graphSettings,
            BaseTheme baseTheme,
            bool viewNames)
        {
            ArgumentNullException.ThrowIfNull(arrowGraph);
            ArgumentNullException.ThrowIfNull(graphSettings);
            DiagramGraphModel diagramGraph = BuildGraphDiagram(arrowGraph, graphSettings, viewNames: viewNames);

            // Fill the graph.
            var drawingGraph = new Microsoft.Msagl.Drawing.Graph();

            foreach (DiagramNodeModel diagramNode in diagramGraph.Nodes)
            {
                var drawingGraphNode = new Microsoft.Msagl.Drawing.Node($@"{diagramNode.Id}");
                drawingGraph.AddNode(drawingGraphNode);
            }

            Dictionary<string, Microsoft.Msagl.Drawing.Node> drawingNodeLookup = drawingGraph.Nodes.ToDictionary(x => x.Id);

            // Track edge -> activity ID for hit-rect computation.
            var edgeToActivityId = new Dictionary<Microsoft.Msagl.Drawing.Edge, int>();

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
                edgeToActivityId[edge] = diagramEdge.Id;
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
                drawingGraphNode.Label.FontColor = s_NodeLabelColor;
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

            byte[] svgData = m_MsaglSvgRenderer.RenderToSvg(drawingGraph, baseTheme);
            IReadOnlyList<GraphEdgeHitRect> edgeHitRects = ComputeEdgeHitRects(drawingGraph, edgeToActivityId);

            return (svgData, edgeHitRects);
        }

        public byte[] BuildArrowGraphMLData(
            ArrowGraphModel arrowGraph,
            GraphSettingsModel graphSettings,
            bool viewNames)
        {
            DiagramGraphModel diagramGraph = BuildGraphDiagram(arrowGraph, graphSettings, multiLineEdgeLabels: true, viewNames: viewNames);
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
            GraphSettingsModel graphSettings,
            bool viewNames)
        {
            DiagramGraphModel diagramGraph = BuildGraphDiagram(arrowGraph, graphSettings, multiLineEdgeLabels: true, viewNames: viewNames);
            string graphviz = GraphVizBuilder.ToGraphViz(diagramGraph);
            return graphviz.StringToByteArray();
        }
    }
}
