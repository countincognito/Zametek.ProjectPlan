using Avalonia.Media;
using System.Text;
using System.Xml.Serialization;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class VertexGraphSerializer
        : IVertexGraphSerializer
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

        private static readonly double s_SvgNodeWidth = 38.0;
        private static readonly double s_SvgNodeHeight = 30.0;
        private static readonly double s_SvgNodeLabelWidth = 30.0;
        private static readonly double s_SvgNodeLabelLines = 3.0;
        private static readonly double s_SvgRadiusInXDirection = 3.0;
        private static readonly double s_SvgRadiusInYDirection = 2.0;
        private static readonly double s_SvgNodeLineThicknessCorrectionFactor = 1.5;

        private static readonly double s_SvgEdgeLabelFontSize = 12.0;
        private static readonly double s_SvgEdgeLabelHeight = 12.0;
        private static readonly Microsoft.Msagl.Drawing.FontStyle s_SvgNodeFontStyle = Microsoft.Msagl.Drawing.FontStyle.Bold;

        private static readonly double s_DiagramNodeModelHeight = 60;
        private static readonly double s_DiagramNodeModelWidth = 86.0;

        // These need to be worked out through trial and error
        // whenever s_SvgNodeLabelWidth is changed.
        private static readonly double s_SvgConsolasLabelWidthCorrectionFactor = s_SvgNodeLabelLines * s_SvgNodeLabelWidth / 11.5;
        private static readonly double s_SvgConsolasLabelHeightCorrectionFactor = 3;

        private static readonly Color s_NodeFillColor = Colors.LightGray;
        private static readonly Color s_NodeBorderColor = Colors.Black;

        private const double c_PxPerInch = 96;
        private const double c_PtPerInch = 72;
        private const string c_FontName = @"Consolas";

        private readonly IMsaglSvgRenderer m_MsaglSvgRenderer;

        #endregion

        #region Ctors

        public VertexGraphSerializer(IMsaglSvgRenderer msaglSvgRenderer)
        {
            ArgumentNullException.ThrowIfNull(msaglSvgRenderer);
            m_MsaglSvgRenderer = msaglSvgRenderer;
        }

        #endregion

        #region Private Methods

        private static string BuildNodeLabel(ActivityModel activityModel)
        {
            ArgumentNullException.ThrowIfNull(activityModel);
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

        private static DiagramNodeModel BuildDiagramNode(
            ActivityNodeModel activityNode,
            GraphNodeBorderFormatLookup nodeFormatLookup,
            SlackColorFormatLookup colorFormatLookup)
        {
            ArgumentNullException.ThrowIfNull(activityNode);
            ArgumentNullException.ThrowIfNull(nodeFormatLookup);
            ArgumentNullException.ThrowIfNull(colorFormatLookup);
            ActivityModel activityModel = activityNode.Content;

            bool isDummy = activityModel.IsDummy();
            bool isCritical = activityModel.IsCritical();

            return new DiagramNodeModel
            {
                Id = activityModel.Id,
                Height = s_DiagramNodeModelHeight,
                Width = s_DiagramNodeModelWidth,
                FillColorHexCode = ColorHelper.ColorToHtmlHexCode(s_NodeFillColor),
                BorderColorHexCode = ColorHelper.ColorFormatToHtmlHexCode(colorFormatLookup.FindSlackColorFormat(activityModel.TotalSlack)),
                BorderDashStyle = nodeFormatLookup.FindGraphNodeBorderDashStyle(isCritical, isDummy),
                BorderThickness = nodeFormatLookup.FindBorderThickness(isCritical, isDummy) * s_SvgNodeLineThicknessCorrectionFactor,
                Text = BuildNodeLabel(activityModel),
                Name = activityModel.Name,
            };
        }

        private static DiagramGraphModel BuildGraphDiagram(
            VertexGraphModel vertexGraphModel,
            GraphSettingsModel graphSettingsModel,
            bool multiLineEdgeLabels = false,
            bool viewNames = false)
        {
            ArgumentNullException.ThrowIfNull(vertexGraphModel);
            ArgumentNullException.ThrowIfNull(graphSettingsModel);
            // Perform validity check.
            IList<ActivityNodeModel> nodeModels = vertexGraphModel.Nodes;
            IDictionary<int, ActivityNodeModel> nodeModelLookup = nodeModels.ToDictionary(x => x.Content.Id);

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
            IList<EventEdgeModel> edgeModels = vertexGraphModel.Edges;
            IDictionary<int, EventEdgeModel> edgeModelLookup = edgeModels.ToDictionary(x => x.Content.Id);
            IEnumerable<int> edgeIds = edgeModelLookup.Keys;

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

            var nodeFormatLookup = new GraphNodeBorderFormatLookup(graphSettingsModel.NodeTypeFormats);
            var edgeFormatLookup = new GraphEdgeFormatLookup(graphSettingsModel.EdgeTypeFormats);
            var colorFormatLookup = new SlackColorFormatLookup(graphSettingsModel.ActivitySeverities);

            // Fill the graph.
            List<DiagramNodeModel> diagramNodeModels = nodeModels.Select(x => BuildDiagramNode(x, nodeFormatLookup, colorFormatLookup)).ToList();
            List<DiagramEdgeModel> diagramEdgeModels = [];

            foreach (EventEdgeModel eventEdge in edgeModels)
            {
                EventModel eventModel = eventEdge.Content;
                int eventId = eventModel.Id;

                // Source == tail
                // Target == head
                var diagramEdgeModel = new DiagramEdgeModel
                {
                    Id = eventId,
                    SourceId = edgeTailNodeLookup[eventId],
                    TargetId = edgeHeadNodeLookup[eventId],
                    DashStyle = edgeFormatLookup.FindGraphEdgeDashStyle(false, false),
                    //ForegroundColorHexCode = ColorHelper.ColorFormatToHtmlHexCode(colorFormatLookup.FindSlackColorFormat(eventModel.TotalSlack)),
                    StrokeThickness = edgeFormatLookup.FindStrokeThickness(false, false),
                    Label = string.Empty,
                    ShowLabel = false
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

        public byte[] BuildVertexGraphSvgData(
            VertexGraphModel vertexGraph,
            GraphSettingsModel graphSettings,
            BaseTheme baseTheme,
            bool viewNames)
        {
            ArgumentNullException.ThrowIfNull(vertexGraph);
            ArgumentNullException.ThrowIfNull(graphSettings);
            DiagramGraphModel diagramGraph = BuildGraphDiagram(vertexGraph, graphSettings, viewNames: viewNames);

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
            drawingGraph.LayoutAlgorithmSettings.EdgeRoutingSettings.EdgeRoutingMode = Microsoft.Msagl.Core.Routing.EdgeRoutingMode.Spline;

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

            return m_MsaglSvgRenderer.RenderToSvg(drawingGraph, baseTheme);
        }

        public byte[] BuildVertexGraphMLData(
            VertexGraphModel vertexGraph,
            GraphSettingsModel graphSettings,
            bool viewNames)
        {
            DiagramGraphModel diagramGraph = BuildGraphDiagram(vertexGraph, graphSettings, multiLineEdgeLabels: true, viewNames: viewNames);
            graphml graphML = GraphMLBuilder.ToGraphML(diagramGraph);
            using var ms = new MemoryStream();
            var xmlSerializer = new XmlSerializer(typeof(graphml));
            xmlSerializer.Serialize(ms, graphML);
            ms.Position = 0;
            using var sr = new StreamReader(ms);
            string content = sr.ReadToEnd();
            return content.StringToByteArray();
        }

        public byte[] BuildVertexGraphVizData(
            VertexGraphModel vertexGraph,
            GraphSettingsModel graphSettings,
            bool viewNames)
        {
            DiagramGraphModel diagramGraph = BuildGraphDiagram(vertexGraph, graphSettings, multiLineEdgeLabels: true, viewNames: viewNames);
            string graphviz = GraphVizBuilder.ToGraphViz(diagramGraph);
            return graphviz.StringToByteArray();
        }
    }
}
