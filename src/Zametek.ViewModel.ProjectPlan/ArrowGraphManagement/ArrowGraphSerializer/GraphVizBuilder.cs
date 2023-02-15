using System.Text;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class GraphVizBuilder
    {
        public static string ToGraphViz(DiagramArrowGraphModel diagramArrowGraph)
        {
            ArgumentNullException.ThrowIfNull(diagramArrowGraph);
            var sb = new StringBuilder();

            _ = sb.Append("digraph Arrow {\nrankdir=LR;\n");

            _ = sb.Append("node [ shape=oval height=.3 width=.1 style=filled fillcolor=\"#e7e7e7\" fontsize=8 fontname=\"Sans-Serif\" penwidth=0 ]\n");

            foreach (var node in diagramArrowGraph.Nodes)
            {
                var nodeOutput = $"\"{node.Id}\" [ label=\"{node.Text}\" ];\n";
                _ = sb.Append(nodeOutput);
            }

            foreach (var edge in diagramArrowGraph.Edges)
            {
                var tooltip = edge.Name;
                var style = edge.DashStyle switch
                {
                    EdgeDashStyle.Normal => @"solid",
                    EdgeDashStyle.Dashed => @"dashed",
                    _ => throw new NotSupportedException($@"{edge.DashStyle} is not supported"),
                };
                var label = edge.ShowLabel ? edge.Label : string.Empty;
                var edgeColor = edge.ForegroundColorHexCode;

                var activity = $"\"{edge.SourceId}\" -> \"{edge.TargetId}\" [ id={edge.Id} style={style} edgetooltip=\"{tooltip}\" labeltooltip=\"{tooltip}\" color=\"{edgeColor}\" fontsize=8 fontname=\"Sans-Serif\" label=\"{label}\" ];\n";

                _ = sb.Append(activity);
            }

            _ = sb.Append('}');

            return sb.ToString();
        }
    }
}
