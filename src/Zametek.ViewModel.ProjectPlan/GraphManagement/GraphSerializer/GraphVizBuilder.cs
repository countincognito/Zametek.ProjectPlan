using System.Text;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class GraphVizBuilder
    {
        public static string ToGraphViz(DiagramGraphModel diagramGraph)
        {
            ArgumentNullException.ThrowIfNull(diagramGraph);
            var sb = new StringBuilder();

            _ = sb.Append("digraph Arrow {\nrankdir=\"LR\";\n");

            foreach (var node in diagramGraph.Nodes)
            {
                string tooltip = node.Name ?? string.Empty;
                string style = node.BorderDashStyle switch
                {
                    NodeBorderDashStyle.Normal => @"solid",
                    NodeBorderDashStyle.Dashed => @"dashed",
                    _ => throw new NotSupportedException($@"{node.BorderDashStyle} is not supported"),
                };

                string label = node.Text ?? string.Empty;
                double nodeBorderWidth = node.BorderThickness;
                string nodeBorderColor = node.BorderColorHexCode ?? @"black";

                string nodeOutput = $"\"{node.Id}\" [ label=\"{label}\" tooltip=\"{tooltip}\" shape=\"rectangle\" height=.3 width=.1 style=\"{style},filled,rounded\" fillcolor=\"#e7e7e7\" fontsize=8 fontname=\"Consolas\" penwidth={nodeBorderWidth} color=\"{nodeBorderColor}\" ];\n";
                _ = sb.Append(nodeOutput);
            }

            foreach (var edge in diagramGraph.Edges)
            {
                string tooltip = edge.Name ?? string.Empty;
                string style = edge.DashStyle switch
                {
                    EdgeDashStyle.Normal => @"solid",
                    EdgeDashStyle.Dashed => @"dashed",
                    _ => throw new NotSupportedException($@"{edge.DashStyle} is not supported"),
                };
                string label = edge.ShowLabel ? edge.Label ?? string.Empty : string.Empty;
                string edgeColor = edge.ForegroundColorHexCode ?? string.Empty;

                string activity = $"\"{edge.SourceId}\" -> \"{edge.TargetId}\" [ id=\"{edge.Id}\" style=\"{style}\" edgetooltip=\"{tooltip}\" labeltooltip=\"{tooltip}\" color=\"{edgeColor}\" fontsize=8 fontname=\"Consolas\" label=\"{label}\" ];\n";

                _ = sb.Append(activity);
            }

            _ = sb.Append('}');

            return sb.ToString();
        }
    }
}
