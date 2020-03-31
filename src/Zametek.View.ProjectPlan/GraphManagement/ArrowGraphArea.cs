using GraphX.Controls;
using QuickGraph;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using Zametek.Common.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class ArrowGraphArea
        : GraphArea<ArrowGraphVertex, ArrowGraphEdge, BidirectionalGraph<ArrowGraphVertex, ArrowGraphEdge>>
    {
        #region Public Methods

        public DiagramArrowGraphModel ToDiagramArrowGraph()
        {
            // Add nodes.
            IList<VertexControl> vertexControls = VertexList.Values.ToList();
            var nodes = vertexControls.Select(BuildDiagramNode).ToList();

            // Add edges.
            IList<EdgeControl> edgeControls = EdgesList.Values.ToList();
            var edges = edgeControls.Select(BuildDiagramEdge).ToList();

            return new DiagramArrowGraphModel
            {
                Nodes = nodes,
                Edges = edges
            };
        }

        #endregion

        #region Private Methods

        private static DiagramNodeModel BuildDiagramNode(VertexControl vertexControl)
        {
            if (vertexControl == null)
            {
                throw new ArgumentNullException(nameof(vertexControl));
            }
            var outputNode = new DiagramNodeModel();
            if (vertexControl.Vertex is ArrowGraphVertex node)
            {
                outputNode.Id = Convert.ToInt32(node.ID);
                Point point = vertexControl.GetPosition();
                outputNode.X = point.X;
                outputNode.Y = point.Y;
                outputNode.Height = vertexControl.ActualHeight;
                outputNode.Width = vertexControl.ActualWidth;
                Color fillColor = ((SolidColorBrush)vertexControl.Background).Color;
                outputNode.FillColorHexCode =
                    ViewModel.ProjectPlan.Converter.HexConverter(fillColor.R, fillColor.G, fillColor.B);
                Color borderColor = ((SolidColorBrush)vertexControl.BorderBrush).Color;
                outputNode.BorderColorHexCode =
                    ViewModel.ProjectPlan.Converter.HexConverter(borderColor.R, borderColor.G, borderColor.B);
                outputNode.Text = node.ToString();
            }
            return outputNode;
        }

        private static DiagramEdgeModel BuildDiagramEdge(EdgeControl edgeControl)
        {
            if (edgeControl == null)
            {
                throw new ArgumentNullException(nameof(edgeControl));
            }
            var outputEdge = new DiagramEdgeModel();
            if (edgeControl.Edge is ArrowGraphEdge edge)
            {
                outputEdge.Id = Convert.ToInt32(edge.ID);
                outputEdge.Name = edge.Name;
                outputEdge.SourceId = Convert.ToInt32(edge.Source.ID);
                outputEdge.TargetId = Convert.ToInt32(edge.Target.ID);

                Common.ProjectPlan.EdgeDashStyle dashStyle;
                switch (edge.DashStyle)
                {
                    case GraphX.Controls.EdgeDashStyle.Solid:
                        dashStyle = Common.ProjectPlan.EdgeDashStyle.Normal;
                        break;
                    case GraphX.Controls.EdgeDashStyle.Dash:
                        dashStyle = Common.ProjectPlan.EdgeDashStyle.Dashed;
                        break;
                    default:
                        throw new InvalidOperationException($@"Unknown EdgeDashStyle value ""{edge.DashStyle}""");
                }

                outputEdge.DashStyle = dashStyle;
                Color foregroundColor = ((SolidColorBrush)edgeControl.Foreground).Color;
                outputEdge.ForegroundColorHexCode =
                    ViewModel.ProjectPlan.Converter.HexConverter(foregroundColor.R, foregroundColor.G, foregroundColor.B);
                outputEdge.StrokeThickness = edge.StrokeThickness;
                var labelText = new StringBuilder();
                if (edge.IsDummy)
                {
                    if (!edge.CanBeRemoved)
                    {
                        labelText.AppendFormat(CultureInfo.InvariantCulture, "{0}", edge.ID);
                        if (!edge.IsCritical)
                        {
                            labelText.AppendLine();
                            labelText.AppendFormat(CultureInfo.InvariantCulture, "{0}|{1}", edge.FreeSlack, edge.TotalSlack);
                        }
                        outputEdge.ShowLabel = true;
                    }
                    else
                    {
                        if (!edge.IsCritical)
                        {
                            labelText.AppendFormat(CultureInfo.InvariantCulture, "{0}|{1}", edge.FreeSlack, edge.TotalSlack);
                            outputEdge.ShowLabel = true;
                        }
                    }
                }
                else
                {
                    labelText.AppendFormat(CultureInfo.InvariantCulture, "{0} ({1})", edge.ID, edge.Duration);
                    if (!edge.IsCritical)
                    {
                        labelText.AppendLine();
                        labelText.AppendFormat(CultureInfo.InvariantCulture, "{0}|{1}", edge.FreeSlack, edge.TotalSlack);
                    }
                    outputEdge.ShowLabel = true;
                }
                outputEdge.Label = labelText.ToString();
            }
            return outputEdge;
        }

        #endregion
    }
}
