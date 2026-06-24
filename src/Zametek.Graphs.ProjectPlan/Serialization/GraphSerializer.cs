using Avalonia.Media;
using System.Xml.Serialization;
using Zametek.Utility;

namespace Zametek.Graphs.ProjectPlan
{
    // The graph layout/render engine. It consumes a library-neutral DiagramGraphModel (built by the
    // application from its domain graph) and runs the MSAGL layout to produce SVG, GraphML, GraphViz,
    // or the interactive GraphLayoutModel. The per-graph MSAGL tuning constants come from a
    // GraphConfiguration, so a single engine serves both the arrow and vertex graphs. It carries no
    // dependency on the application's domain models - all label text, tooltips, presentation and
    // validation are resolved upstream. (Replaces the parallel ArrowGraphSerializer/VertexGraphSerializer.)
    public class GraphSerializer
        : IGraphSerializer
    {
        #region Fields

        private static readonly Dictionary<GraphDashStyle, Microsoft.Msagl.Drawing.Style> s_DashMsaglLookup =
             new()
             {
                {GraphDashStyle.Normal, Microsoft.Msagl.Drawing.Style.Solid},
                {GraphDashStyle.Dashed, Microsoft.Msagl.Drawing.Style.Dashed}
             };

        // Unit conversions - the only layout constants not carried by the GraphConfiguration.
        private const double c_PxPerInch = 96;
        private const double c_PtPerInch = 72;

        private readonly GraphConfiguration m_Config;
        private readonly IMsaglSvgRenderer m_MsaglSvgRenderer;

        #endregion

        #region Ctors

        public GraphSerializer(
            GraphConfiguration configuration,
            IMsaglSvgRenderer msaglSvgRenderer)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(msaglSvgRenderer);
            m_Config = configuration;
            m_MsaglSvgRenderer = msaglSvgRenderer;
        }

        #endregion

        public byte[] BuildGraphSvgData(DiagramGraphModel diagramGraph, GraphTheme theme)
        {
            Microsoft.Msagl.Drawing.Graph drawingGraph = BuildAndLayoutDrawingGraph(diagramGraph, theme);
            return m_MsaglSvgRenderer.RenderToSvg(drawingGraph, theme);
        }

        public GraphLayoutModel BuildGraphLayout(DiagramGraphModel diagramGraph, GraphTheme theme)
        {
            Microsoft.Msagl.Drawing.Graph drawingGraph = BuildAndLayoutDrawingGraph(diagramGraph, theme);
            return ExtractLayout(drawingGraph, diagramGraph, m_Config.InteractiveLayoutScalingFactor);
        }

        public byte[] BuildGraphMLData(DiagramGraphModel diagramGraph)
        {
            ArgumentNullException.ThrowIfNull(diagramGraph);
            graphml graphML = GraphMLBuilder.ToGraphML(diagramGraph);
            using var ms = new MemoryStream();
            var xmlSerializer = new XmlSerializer(typeof(graphml));
            xmlSerializer.Serialize(ms, graphML);
            ms.Position = 0;
            using var sr = new StreamReader(ms);
            string content = sr.ReadToEnd();
            return content.StringToByteArray();
        }

        public byte[] BuildGraphVizData(DiagramGraphModel diagramGraph)
        {
            ArgumentNullException.ThrowIfNull(diagramGraph);
            string graphviz = GraphVizBuilder.ToGraphViz(diagramGraph);
            return graphviz.StringToByteArray();
        }

        #region Private Methods

        private Microsoft.Msagl.Drawing.Graph BuildAndLayoutDrawingGraph(
            DiagramGraphModel diagramGraph,
            GraphTheme theme)
        {
            ArgumentNullException.ThrowIfNull(diagramGraph);

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
                edge.Attr.AddStyle(s_DashMsaglLookup[diagramEdge.DashStyle]);
                edge.Attr.Color = HtmlHexCodeToMsaglColor(diagramEdge.ForegroundColorHexCode) ?? Microsoft.Msagl.Drawing.Color.Black;
                edge.Attr.LineWidth = diagramEdge.StrokeThickness;
                edge.LabelText = diagramEdge.Label;
                edge.Label.IsVisible = diagramEdge.ShowLabel;
                edge.Label.FontColor = EdgeFontColor(theme);

                drawingGraph.AddPrecalculatedEdge(edge);
            }

            drawingGraph.LayoutAlgorithmSettings = drawingGraph.CreateLayoutSettings();

            drawingGraph.LayoutAlgorithmSettings.EdgeRoutingSettings.UseObstacleRectangles = true;
            drawingGraph.LayoutAlgorithmSettings.EdgeRoutingSettings.EdgeRoutingMode = MapRoutingMode(m_Config.EdgeRoutingMode);

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
                double nodeLabelFontSize = m_Config.LabelWidthCorrectionFactor * m_Config.NodeLabelWidth * c_PtPerInch / (drawingGraphNode.LabelText.Length * c_PxPerInch);
                double nodeLabelHeight = m_Config.LabelHeightCorrectionFactor * nodeLabelFontSize * c_PxPerInch / c_PtPerInch;
                double nodeHeight = m_Config.NodeHeight;

                drawingGraphNode.GeometryNode.BoundaryCurve =
                    Microsoft.Msagl.Core.Geometry.Curves.CurveFactory.CreateRectangleWithRoundedCorners(
                        m_Config.NodeWidth,
                        nodeHeight,
                        m_Config.NodeCornerRadiusX,
                        m_Config.NodeCornerRadiusY,
                        new Microsoft.Msagl.Core.Geometry.Point(0, 0));

                drawingGraphNode.Label.Height = nodeLabelHeight;
                drawingGraphNode.Label.Width = m_Config.NodeLabelWidth;
                drawingGraphNode.Label.FontSize = nodeLabelFontSize;
                drawingGraphNode.Label.FontStyle = MapFontStyle(m_Config.NodeFontStyle);

                drawingGraphNode.Label.FontName = m_Config.FontName;
                drawingGraphNode.Attr.AddStyle(s_DashMsaglLookup[diagramNode.BorderDashStyle]);
                drawingGraphNode.Attr.FillColor = HtmlHexCodeToMsaglColor(diagramNode.FillColorHexCode) ?? Microsoft.Msagl.Drawing.Color.LightGray;
                drawingGraphNode.Attr.Color = HtmlHexCodeToMsaglColor(diagramNode.BorderColorHexCode) ?? Microsoft.Msagl.Drawing.Color.Black;
                drawingGraphNode.Attr.LineWidth = diagramNode.BorderThickness;
            }

            // Initialise geometry labels as well.
            foreach (Microsoft.Msagl.Drawing.Edge drawingGraphEdge in drawingGraph.Edges)
            {
                double edgeLabelWidth = drawingGraphEdge.LabelText.Length * m_Config.EdgeLabelFontSize * (c_PxPerInch / c_PtPerInch) / m_Config.LabelWidthCorrectionFactor;

                drawingGraphEdge.Label.FontName = m_Config.FontName;
                drawingGraphEdge.Label.FontSize = m_Config.EdgeLabelFontSize;
                drawingGraphEdge.Label.GeometryLabel.Width = edgeLabelWidth;
                drawingGraphEdge.Label.GeometryLabel.Height = m_Config.EdgeLabelHeight;
                drawingGraphEdge.Label.GeometryLabel.Center = new Microsoft.Msagl.Core.Geometry.Point(0, 0);
                drawingGraphEdge.Label.GeometryLabel.PlacementResult = Microsoft.Msagl.Core.Layout.LabelPlacementResult.OverlapsNothing;
            }

            Microsoft.Msagl.Miscellaneous.LayoutHelpers.CalculateLayout(drawingGraph.GeometryGraph, drawingGraph.LayoutAlgorithmSettings, null);

            return drawingGraph;
        }

        private static GraphLayoutModel ExtractLayout(
            Microsoft.Msagl.Drawing.Graph drawingGraph,
            DiagramGraphModel diagramGraph,
            double interactiveLayoutScalingFactor)
        {
            Microsoft.Msagl.Core.Geometry.Rectangle boundingBox = drawingGraph.GeometryGraph.BoundingBox;
            double graphLeft = boundingBox.Left;
            double graphTop = boundingBox.Top; // Largest Y in MSAGL's Y-up space.

            Dictionary<int, DiagramNodeModel> diagramNodeLookup = diagramGraph.Nodes.ToDictionary(x => x.Id);

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

                nodes.Add(new GraphNodeLayoutModel
                {
                    Id = id,
                    X = (centreX - (width / 2.0)) * interactiveLayoutScalingFactor,
                    Y = (centreY - (height / 2.0)) * interactiveLayoutScalingFactor,
                    Width = width * interactiveLayoutScalingFactor,
                    Height = height * interactiveLayoutScalingFactor,
                    Label = diagramNode.Text ?? string.Empty,
                    Name = diagramNode.Name,
                    Tooltip = diagramNode.Tooltip,
                    FillColorHexCode = diagramNode.FillColorHexCode,
                    BorderColorHexCode = diagramNode.BorderColorHexCode,
                    BorderThickness = diagramNode.BorderThickness,
                    IsDashed = diagramNode.BorderDashStyle == GraphDashStyle.Dashed,
                });
            }

            List<GraphEdgeLayoutModel> edges = diagramGraph.Edges
                .Select(x => new GraphEdgeLayoutModel
                {
                    Id = x.Id,
                    SourceId = x.SourceId,
                    TargetId = x.TargetId,
                    StrokeThickness = x.StrokeThickness,
                    IsDashed = x.DashStyle == GraphDashStyle.Dashed,
                    ForegroundColorHexCode = x.ForegroundColorHexCode,
                    Label = x.Label,
                    ShowLabel = x.ShowLabel,
                    Tooltip = x.Tooltip,
                })
                .ToList();

            return new GraphLayoutModel
            {
                Width = boundingBox.Width * interactiveLayoutScalingFactor,
                Height = boundingBox.Height * interactiveLayoutScalingFactor,
                Nodes = nodes,
                Edges = edges,
            };
        }

        private static Microsoft.Msagl.Drawing.FontStyle MapFontStyle(GraphNodeFontStyle nodeFontStyle)
        {
            return nodeFontStyle == GraphNodeFontStyle.Bold
                ? Microsoft.Msagl.Drawing.FontStyle.Bold
                : Microsoft.Msagl.Drawing.FontStyle.Regular;
        }

        // One-for-one with Microsoft.Msagl.Core.Routing.EdgeRoutingMode, so the fixed-layout SVG
        // export honours every mode exactly (the interactive view approximates them client-side).
        private static Microsoft.Msagl.Core.Routing.EdgeRoutingMode MapRoutingMode(GraphEdgeRoutingMode edgeRoutingMode)
        {
            return edgeRoutingMode switch
            {
                GraphEdgeRoutingMode.Spline => Microsoft.Msagl.Core.Routing.EdgeRoutingMode.Spline,
                GraphEdgeRoutingMode.SplineBundling => Microsoft.Msagl.Core.Routing.EdgeRoutingMode.SplineBundling,
                GraphEdgeRoutingMode.StraightLine => Microsoft.Msagl.Core.Routing.EdgeRoutingMode.StraightLine,
                GraphEdgeRoutingMode.SugiyamaSplines => Microsoft.Msagl.Core.Routing.EdgeRoutingMode.SugiyamaSplines,
                GraphEdgeRoutingMode.Rectilinear => Microsoft.Msagl.Core.Routing.EdgeRoutingMode.Rectilinear,
                GraphEdgeRoutingMode.RectilinearToCenter => Microsoft.Msagl.Core.Routing.EdgeRoutingMode.RectilinearToCenter,
                GraphEdgeRoutingMode.None => Microsoft.Msagl.Core.Routing.EdgeRoutingMode.None,
                _ => Microsoft.Msagl.Core.Routing.EdgeRoutingMode.SugiyamaSplines,
            };
        }

        private static Microsoft.Msagl.Drawing.Color? HtmlHexCodeToMsaglColor(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            Color color = ColorHelper.HtmlHexCodeToColor(input);

            return new Microsoft.Msagl.Drawing.Color
            {
                A = color.A,
                R = color.R,
                G = color.G,
                B = color.B
            };
        }

        private static Microsoft.Msagl.Drawing.Color EdgeFontColor(GraphTheme theme)
        {
            if (theme == GraphTheme.Light)
            {
                return Microsoft.Msagl.Drawing.Color.Black;
            }
            if (theme == GraphTheme.Dark)
            {
                return Microsoft.Msagl.Drawing.Color.White;
            }
            return Microsoft.Msagl.Drawing.Color.Black;
        }

        #endregion
    }
}
