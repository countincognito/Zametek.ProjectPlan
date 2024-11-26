using Avalonia.Media;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ArrowGraphSerializer
        : IArrowGraphSerializer
    {
        #region Fields

        private static readonly IDictionary<EdgeDashStyle, Microsoft.Msagl.Drawing.Style> s_EdgeDashMsaglLookup =
             new Dictionary<EdgeDashStyle, Microsoft.Msagl.Drawing.Style>
             {
                {EdgeDashStyle.Normal, Microsoft.Msagl.Drawing.Style.Solid},
                {EdgeDashStyle.Dashed, Microsoft.Msagl.Drawing.Style.Dashed}
             };

        private static readonly double s_SvgNodeWidth = 40.0;
        private static readonly double s_SvgNodeHeight = 34.0;
        private static readonly double s_SvgNodeLabelWidth = 34.0;
        private static readonly double s_SvgRadiusInXDirection = 3.0;
        private static readonly double s_SvgRadiusInYDirection = 2.0;

        private static readonly double s_SvgEdgeLabelFontSize = 12.0;
        private static readonly double s_SvgEdgeLabelHeight = 12.0;


        private static readonly double s_DiagramNodeModelHeight = 26.0;
        private static readonly double s_DiagramNodeModelWidth = 62.0;

        // These need to be worked out through trial and error
        // whenever s_SvgNodeLabelWidth is changed.
        private static readonly double s_SvgConsolasLabelWidthCorrectionFactor = s_SvgNodeLabelWidth / 14;
        private static readonly double s_SvgConsolasLabelHeightCorrectionFactor = 0.7;

        private static readonly Color s_NodeFillColor = Colors.LightGray;
        private static readonly Color s_NodeBorderColor = Colors.Black;

        private const double c_PxPerInch = 96;
        private const double c_PtPerInch = 72;
        private const string c_FontName = @"Consolas";

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

            return new DiagramNodeModel
            {
                Id = eventModel.Id,
                Height = s_DiagramNodeModelHeight,
                Width = s_DiagramNodeModelWidth,
                FillColorHexCode = ColorHelper.ColorToHtmlHexCode(s_NodeFillColor),
                BorderColorHexCode = ColorHelper.ColorToHtmlHexCode(s_NodeBorderColor),
                Text = BuildNodeLabel(eventModel)
            };

            //Point point = vertexControl.GetPosition();
            //outputNode.X = point.X;
            //outputNode.Y = point.Y;

        }

        private static (bool isVisible, string labelText) BuildSingleLineEdgeLabel(ActivityModel activityModel)
        {
            ArgumentNullException.ThrowIfNull(activityModel);
            var labelText = new StringBuilder();
            bool isVisible = false;

            if (activityModel.IsDummy())
            {
                if (!activityModel.CanBeRemoved)
                {
                    labelText.Append(@$"{activityModel.Id}");
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
                labelText.Append(@$"{activityModel.Id}({activityModel.Duration})");
                if (!activityModel.IsCritical())
                {
                    labelText.Append(@$" [{activityModel.FreeSlack}|{activityModel.TotalSlack}]");
                }
                isVisible = true;
            }
            return (isVisible, labelText.ToString());
        }

        private static (bool isVisible, string labelText) BuildMultiLineEdgeLabel(ActivityModel activityModel)
        {
            ArgumentNullException.ThrowIfNull(activityModel);
            var labelText = new StringBuilder();
            bool isVisible = false;

            if (activityModel.IsDummy())
            {
                if (!activityModel.CanBeRemoved)
                {
                    labelText.AppendFormat($@"{activityModel.Id}");
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
                labelText.AppendFormat($@"{activityModel.Id} ({activityModel.Duration})");
                if (!activityModel.IsCritical())
                {
                    labelText.AppendLine();
                    labelText.AppendFormat($@"{activityModel.FreeSlack}|{activityModel.TotalSlack}");
                }
                isVisible = true;
            }
            return (isVisible, labelText.ToString());
        }

        private static DiagramArrowGraphModel BuildArrowGraphDiagram(
            ArrowGraphModel arrowGraphModel,
            ArrowGraphSettingsModel arrowGraphSettingsModel,
            bool multiLineEdgeLabels = false)
        {
            ArgumentNullException.ThrowIfNull(arrowGraphModel);
            ArgumentNullException.ThrowIfNull(arrowGraphSettingsModel);
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
                throw new ArgumentException(Resource.ProjectPlan.Messages.Message_MismatchedEdgeIdsForHeadNodes);
            }
            if (!edgeIds.OrderBy(x => x).SequenceEqual(edgeTailNodeLookup.Keys.OrderBy(x => x)))
            {
                throw new ArgumentException(Resource.ProjectPlan.Messages.Message_MismatchedEdgeIdsForTailNodes);
            }

            // Check all events are used.
            IEnumerable<int> edgeNodeLookupIds = edgeHeadNodeLookup.Values.Union(edgeTailNodeLookup.Values);

            if (!drawingGraphNodeIds.OrderBy(x => x).SequenceEqual(edgeNodeLookupIds.OrderBy(x => x)))
            {
                throw new ArgumentException(Resource.ProjectPlan.Messages.Message_MismatchedNodeIdsAssociatedWithEdges);
            }

            // Check Start and End nodes.
            IEnumerable<EventNodeModel> startNodes = nodeModels.Where(x => x.NodeType == NodeType.Start);
            if (startNodes.Count() > 1)
            {
                throw new ArgumentException(Resource.ProjectPlan.Messages.Message_ArrowGraphDataContainMultipleStartNodes);
            }

            IEnumerable<EventNodeModel> endNodes = nodeModels.Where(x => x.NodeType == NodeType.End);
            if (endNodes.Count() > 1)
            {
                throw new ArgumentException(Resource.ProjectPlan.Messages.Message_ArrowGraphDataContainMultipleEndNodes);
            }

            var edgeFormatLookup = new GraphEdgeFormatLookup(arrowGraphSettingsModel.EdgeTypeFormats);
            var colorFormatLookup = new SlackColorFormatLookup(arrowGraphSettingsModel.ActivitySeverities);

            // Fill the graph.
            List<DiagramNodeModel> diagramNodeModels = nodeModels.Select(BuildDiagramNode).ToList();
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
                    (showLabel, labelText) = BuildMultiLineEdgeLabel(activityModel);
                }
                else
                {
                    (showLabel, labelText) = BuildSingleLineEdgeLabel(activityModel);
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
                    ShowLabel = showLabel
                };

                diagramEdgeModels.Add(diagramEdgeModel);
            }

            return new DiagramArrowGraphModel
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

        private static byte[] GraphToByteArray(
            Microsoft.Msagl.Drawing.Graph graph,
            BaseTheme baseTheme)
        {
            using var ms = new MemoryStream();
            using var writer = new StreamWriter(ms);

            var svgWriter = new CustomSvgGraphWriter(writer.BaseStream, graph);
            svgWriter.Write();
            ms.Position = 0;
            using var sr = new StreamReader(ms);

            using var xmlReader = XmlReader.Create(sr);

            XmlDocument doc = new();
            doc.Load(xmlReader);

            // Set the background to transparent so it 
            string? height = doc.DocumentElement?.GetAttribute("height");
            string? width = doc.DocumentElement?.GetAttribute("width");

            var rect = doc.CreateElement(@"rect");
            rect.SetAttribute(@"height", height);
            rect.SetAttribute(@"width", width);

            if (baseTheme == BaseTheme.Light)
            {
                rect.SetAttribute(@"fill", ColorHelper.SvgLightThemeBackground);
            }
            if (baseTheme == BaseTheme.Dark)
            {
                rect.SetAttribute(@"fill", ColorHelper.SvgDarkThemeBackground);
            }

            // Only show the background if there is a graph to display.
            if (graph.NodeCount > 0)
            {
                rect.SetAttribute(@"fill-opacity", "1.0");
            }
            else
            {
                rect.SetAttribute(@"fill-opacity", "0.0");
            }

            // Add the background to the top of the XML tree.
            doc.DocumentElement?.PrependChild(rect);

            using var stringWriter = new StringWriter();
            using var xmlTextWriter = XmlWriter.Create(stringWriter);

            doc.WriteTo(xmlTextWriter);
            xmlTextWriter.Flush();

            return stringWriter
                .GetStringBuilder()
                .ToString()
                .StringToByteArray();
        }

        #endregion

        public byte[] BuildArrowGraphSvgData(
            ArrowGraphModel arrowGraph,
            ArrowGraphSettingsModel arrowGraphSettings,
            BaseTheme baseTheme)
        {
            ArgumentNullException.ThrowIfNull(arrowGraph);
            ArgumentNullException.ThrowIfNull(arrowGraphSettings);
            DiagramArrowGraphModel diagramArrowGraph = BuildArrowGraphDiagram(arrowGraph, arrowGraphSettings);

            // Fill the graph.
            var drawingGraph = new Microsoft.Msagl.Drawing.Graph();

            foreach (DiagramNodeModel diagramNode in diagramArrowGraph.Nodes)
            {
                var drawingGraphNode = new Microsoft.Msagl.Drawing.Node($@"{diagramNode.Id}");
                drawingGraph.AddNode(drawingGraphNode);
            }

            Dictionary<string, Microsoft.Msagl.Drawing.Node> drawingNodeLookup = drawingGraph.Nodes.ToDictionary(x => x.Id);

            foreach (DiagramEdgeModel diagramEdge in diagramArrowGraph.Edges)
            {
                var edge = new Microsoft.Msagl.Drawing.Edge(
                    drawingNodeLookup[$@"{diagramEdge.SourceId}"],
                    drawingNodeLookup[$@"{diagramEdge.TargetId}"],
                    Microsoft.Msagl.Drawing.ConnectionToGraph.Connected);

                edge.Attr.ClearStyles();
                edge.Attr.AddStyle(s_EdgeDashMsaglLookup[diagramEdge.DashStyle]);
                edge.Attr.Color = HtmlHexCodeToMsaglColor(diagramEdge.ForegroundColorHexCode) ?? Microsoft.Msagl.Drawing.Color.Black;
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

            Dictionary<string, DiagramNodeModel> diagramNodeLookup = diagramArrowGraph.Nodes.ToDictionary(x => $@"{x.Id}");

            // Fill the nodes.
            foreach (Microsoft.Msagl.Drawing.Node drawingGraphNode in drawingGraph.Nodes)
            {
                DiagramNodeModel diagramNode = diagramNodeLookup[drawingGraphNode.Id];
                //EventNodeModel nodeModel = nodeModelLookup[drawingGraphNode.Id];

                // Make sure this is set before the label is updated.
                drawingGraphNode.LabelText = diagramNode.Text ?? string.Empty;

                // Calculate the correct label font size (Pts) and the label height (Pxs)
                // based off of the pt->px conversion, with Consolas correction factors.
                double nodeLabelFontSize = s_SvgConsolasLabelWidthCorrectionFactor * s_SvgNodeLabelWidth * c_PtPerInch / (drawingGraphNode.LabelText.Length * c_PxPerInch);
                double nodeLabelHeight = s_SvgConsolasLabelHeightCorrectionFactor * nodeLabelFontSize * c_PxPerInch / c_PtPerInch;
                double nodeHeight = s_SvgNodeHeight;// nodeLabelHeight / s_ConsolasLabelHeightCorrectionFactor;

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

                drawingGraphNode.Label.FontName = c_FontName;
                drawingGraphNode.Attr.FillColor = HtmlHexCodeToMsaglColor(diagramNode.FillColorHexCode) ?? Microsoft.Msagl.Drawing.Color.LightGray;
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

            return GraphToByteArray(drawingGraph, baseTheme);
        }

        public byte[] BuildArrowGraphMLData(
            ArrowGraphModel arrowGraph,
            ArrowGraphSettingsModel arrowGraphSettings)
        {
            DiagramArrowGraphModel diagramArrowGraph = BuildArrowGraphDiagram(arrowGraph, arrowGraphSettings, multiLineEdgeLabels: true);
            graphml graphML = GraphMLBuilder.ToGraphML(diagramArrowGraph);
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
            ArrowGraphSettingsModel arrowGraphSettings)
        {
            DiagramArrowGraphModel diagramArrowGraph = BuildArrowGraphDiagram(arrowGraph, arrowGraphSettings, multiLineEdgeLabels: true);
            string graphviz = GraphVizBuilder.ToGraphViz(diagramArrowGraph);
            return graphviz.StringToByteArray();
        }
    }
}
