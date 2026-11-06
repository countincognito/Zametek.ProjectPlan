using Avalonia.Media;

namespace Zametek.Graphs.Avalonia
{
    // The Microsoft.Msagl-backed implementation of IGraphLayoutEngine: it builds an MSAGL drawing graph
    // from the library-neutral DiagramGraphModel, runs the MSAGL layout, and produces either the
    // interactive GraphLayoutModel (resolved coordinates the interactive control places its controls
    // at) or a fixed-layout SVG (via the internal MsaglSvgRenderer). This is the ONLY layout/render
    // component coupled to the graph-layout library - the abstractions stay framework-neutral, so a
    // consumer could substitute a different engine. The per-graph MSAGL tuning comes from the supplied
    // GraphConfiguration, so a single (stateless) engine serves both the arrow and vertex graphs.
    // (Extracted from the former GraphSerializer, which kept the Msagl code mixed into its serializer.)
    public class MsaglGraphLayoutEngine
        : IGraphLayoutEngine
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

        #endregion

        #region IGraphLayoutEngine Members

        public GraphLayoutModel BuildLayout(DiagramGraphModel diagramGraph, GraphConfiguration configuration, GraphTheme theme)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            LaidOutDrawingGraph laidOut = BuildAndLayoutDrawingGraph(diagramGraph, configuration, theme);
            return ExtractLayout(laidOut, diagramGraph, configuration.InteractiveLayoutScalingFactor);
        }

        public byte[] RenderSvg(DiagramGraphModel diagramGraph, GraphConfiguration configuration, GraphTheme theme)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            LaidOutDrawingGraph laidOut = BuildAndLayoutDrawingGraph(diagramGraph, configuration, theme);
            return MsaglSvgRenderer.RenderToSvg(laidOut.Graph, laidOut.Nodes, laidOut.Edges, theme);
        }

        #endregion

        #region Label Sizing

        // The label sizes the layout reserves come from the configuration's font correction factors rather
        // than from measuring text, so the layout is the same on every machine. A node label's point size is
        // whatever fits its text into NodeLabelWidth; an edge label's box is as wide as its text at
        // EdgeLabelFontSize. Internal so that the tests can hold both to the bundled font's metrics.
        internal static double NodeLabelFontSize(string labelText, GraphConfiguration config) =>
            config.LabelWidthCorrectionFactor * config.NodeLabelWidth * c_PtPerInch / (labelText.Length * c_PxPerInch);

        internal static double EdgeLabelWidth(string labelText, GraphConfiguration config) =>
            labelText.Length * config.EdgeLabelFontSize * (c_PxPerInch / c_PtPerInch) / config.LabelWidthCorrectionFactor;

        #endregion

        #region Private Types

        // A laid-out MSAGL drawing graph, with its nodes and edges in the diagram's own order. Everything
        // downstream of the layout reads these lists rather than Graph.Nodes/Graph.Edges: the drawing graph
        // keeps its nodes in a Hashtable keyed by the id string, and .NET randomises string hashing per
        // process, so MSAGL's own enumeration order changes from one run of the application to the next.
        private sealed record LaidOutDrawingGraph(
            Microsoft.Msagl.Drawing.Graph Graph,
            IReadOnlyList<Microsoft.Msagl.Drawing.Node> Nodes,
            IReadOnlyList<Microsoft.Msagl.Drawing.Edge> Edges);

        #endregion

        #region Private Methods

        private static LaidOutDrawingGraph BuildAndLayoutDrawingGraph(
            DiagramGraphModel diagramGraph,
            GraphConfiguration config,
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
            List<Microsoft.Msagl.Drawing.Node> drawingNodes = [.. diagramGraph.Nodes.Select(x => drawingNodeLookup[$@"{x.Id}"])];
            var drawingEdges = new List<Microsoft.Msagl.Drawing.Edge>(diagramGraph.Edges.Count);

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
                drawingEdges.Add(edge);
            }

            drawingGraph.LayoutAlgorithmSettings = drawingGraph.CreateLayoutSettings();

            drawingGraph.LayoutAlgorithmSettings.EdgeRoutingSettings.UseObstacleRectangles = true;
            drawingGraph.LayoutAlgorithmSettings.EdgeRoutingSettings.EdgeRoutingMode = MapRoutingMode(config.EdgeRoutingMode);

            drawingGraph.Attr.LayerDirection = Microsoft.Msagl.Drawing.LayerDirection.LR;

            // Draw the graph.
            drawingGraph.CreateGeometryGraph();

            Dictionary<string, DiagramNodeModel> diagramNodeLookup = diagramGraph.Nodes.ToDictionary(x => $@"{x.Id}");

            // Fill the nodes.
            foreach (Microsoft.Msagl.Drawing.Node drawingGraphNode in drawingNodes)
            {
                DiagramNodeModel diagramNode = diagramNodeLookup[drawingGraphNode.Id];

                // Make sure this is set before the label is updated.
                drawingGraphNode.LabelText = diagramNode.Text ?? string.Empty;

                // Calculate the correct label font size (Pts) and the label height (Pxs)
                // based off of the pt->px conversion, with the font's correction factors.
                double nodeLabelFontSize = NodeLabelFontSize(drawingGraphNode.LabelText, config);
                double nodeLabelHeight = config.LabelHeightCorrectionFactor * nodeLabelFontSize * c_PxPerInch / c_PtPerInch;
                double nodeHeight = config.NodeHeight;

                drawingGraphNode.GeometryNode.BoundaryCurve =
                    Microsoft.Msagl.Core.Geometry.Curves.CurveFactory.CreateRectangleWithRoundedCorners(
                        config.NodeWidth,
                        nodeHeight,
                        config.NodeCornerRadiusX,
                        config.NodeCornerRadiusY,
                        new Microsoft.Msagl.Core.Geometry.Point(0, 0));

                drawingGraphNode.Label.Height = nodeLabelHeight;
                drawingGraphNode.Label.Width = config.NodeLabelWidth;
                drawingGraphNode.Label.FontSize = nodeLabelFontSize;
                drawingGraphNode.Label.FontStyle = MapFontStyle(config.NodeFontStyle);

                drawingGraphNode.Label.FontName = config.FontName;
                drawingGraphNode.Attr.AddStyle(s_DashMsaglLookup[diagramNode.BorderDashStyle]);
                drawingGraphNode.Attr.FillColor = HtmlHexCodeToMsaglColor(diagramNode.FillColorHexCode) ?? Microsoft.Msagl.Drawing.Color.LightGray;
                drawingGraphNode.Attr.Color = HtmlHexCodeToMsaglColor(diagramNode.BorderColorHexCode) ?? Microsoft.Msagl.Drawing.Color.Black;
                drawingGraphNode.Attr.LineWidth = diagramNode.BorderThickness;
            }

            // Initialise geometry labels as well.
            foreach (Microsoft.Msagl.Drawing.Edge drawingGraphEdge in drawingEdges)
            {
                double edgeLabelWidth = EdgeLabelWidth(drawingGraphEdge.LabelText, config);

                drawingGraphEdge.Label.FontName = config.FontName;
                drawingGraphEdge.Label.FontSize = config.EdgeLabelFontSize;
                drawingGraphEdge.Label.GeometryLabel.Width = edgeLabelWidth;
                drawingGraphEdge.Label.GeometryLabel.Height = config.EdgeLabelHeight;
                drawingGraphEdge.Label.GeometryLabel.Center = new Microsoft.Msagl.Core.Geometry.Point(0, 0);
                drawingGraphEdge.Label.GeometryLabel.PlacementResult = Microsoft.Msagl.Core.Layout.LabelPlacementResult.OverlapsNothing;
            }

            PutGeometryInDiagramOrder(drawingGraph.GeometryGraph, drawingNodes, drawingEdges);

            Microsoft.Msagl.Miscellaneous.LayoutHelpers.CalculateLayout(drawingGraph.GeometryGraph, drawingGraph.LayoutAlgorithmSettings, null);

            return new LaidOutDrawingGraph(drawingGraph, drawingNodes, drawingEdges);
        }

        // CreateGeometryGraph hands the geometry graph its nodes and edges in the drawing graph's Hashtable
        // order, which changes from one process to the next, and the layered layout breaks its ties by input
        // order - so the same diagram came out differently each time the application started (while staying
        // stable within one run, which is why it went unnoticed). Rebuilding both collections in the
        // diagram's order makes the layout a function of the diagram alone. Clear/Add leave each edge
        // registered exactly once with its source and target. One case this cannot reach: MSAGL's Spline
        // routing (the vertex preset) lays out parallel edges - two edges joining the same pair of nodes -
        // slightly differently on every call, even within one process. The application's graphs never
        // contain parallel edges.
        private static void PutGeometryInDiagramOrder(
            Microsoft.Msagl.Core.Layout.GeometryGraph geometryGraph,
            IReadOnlyList<Microsoft.Msagl.Drawing.Node> drawingNodes,
            IReadOnlyList<Microsoft.Msagl.Drawing.Edge> drawingEdges)
        {
            geometryGraph.Nodes.Clear();
            foreach (Microsoft.Msagl.Drawing.Node drawingNode in drawingNodes)
            {
                geometryGraph.Nodes.Add(drawingNode.GeometryNode);
            }

            geometryGraph.Edges.Clear();
            foreach (Microsoft.Msagl.Drawing.Edge drawingEdge in drawingEdges)
            {
                geometryGraph.Edges.Add(drawingEdge.GeometryEdge);
            }
        }

        private static GraphLayoutModel ExtractLayout(
            LaidOutDrawingGraph laidOut,
            DiagramGraphModel diagramGraph,
            double interactiveLayoutScalingFactor)
        {
            Microsoft.Msagl.Core.Geometry.Rectangle boundingBox = laidOut.Graph.GeometryGraph.BoundingBox;
            double graphLeft = boundingBox.Left;
            double graphTop = boundingBox.Top; // Largest Y in MSAGL's Y-up space.

            Dictionary<int, DiagramNodeModel> diagramNodeLookup = diagramGraph.Nodes.ToDictionary(x => x.Id);

            var nodes = new List<GraphNodeLayoutModel>();

            // In the diagram's order, which the interactive graph's node collection - and so its edge
            // routing requests and the arrangement it persists - inherits.
            foreach (Microsoft.Msagl.Drawing.Node drawingNode in laidOut.Nodes)
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

            List<GraphEdgeLayoutModel> edges = [.. diagramGraph.Edges
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
                })];

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
