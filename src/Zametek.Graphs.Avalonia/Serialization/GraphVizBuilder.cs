using System.Globalization;

namespace Zametek.Graphs.Avalonia
{
    // Dot is a file for other programs to read, so every number in it is written the same whatever the culture:
    // penwidth=1.5, never penwidth=1,5.
    public static class GraphVizBuilder
    {
        public static string ToGraphViz(DiagramGraphModel diagramGraph)
        {
            ArgumentNullException.ThrowIfNull(diagramGraph);
            List<string> lines =
            [
                "digraph Arrow {",
                "rankdir=\"LR\";",
            ];

            foreach (var node in diagramGraph.Nodes)
            {
                string tooltip = Escape(node.Name);
                string style = node.BorderDashStyle switch
                {
                    GraphDashStyle.Normal => @"solid",
                    GraphDashStyle.Dashed => @"dashed",
                    _ => throw new NotSupportedException($@"{node.BorderDashStyle} is not supported"),
                };

                string label = Escape(node.Text);
                double nodeBorderWidth = node.BorderThickness;
                string nodeBorderColor = node.BorderColorHexCode ?? @"black";

                string nodeOutput = string.Create(CultureInfo.InvariantCulture, $"\"{node.Id}\" [ label=\"{label}\" tooltip=\"{tooltip}\" shape=\"rectangle\" height=.3 width=.1 style=\"{style},filled,rounded\" fillcolor=\"#e7e7e7\" fontsize=8 fontname=\"Consolas\" penwidth={nodeBorderWidth} color=\"{nodeBorderColor}\" ];");
                lines.Add(nodeOutput);
            }

            foreach (var edge in diagramGraph.Edges)
            {
                string tooltip = Escape(edge.Name);
                string style = edge.DashStyle switch
                {
                    GraphDashStyle.Normal => @"solid",
                    GraphDashStyle.Dashed => @"dashed",
                    _ => throw new NotSupportedException($@"{edge.DashStyle} is not supported"),
                };
                string label = edge.ShowLabel ? Escape(edge.Label) : string.Empty;
                string edgeColor = edge.ForegroundColorHexCode ?? string.Empty;

                string activity = string.Create(CultureInfo.InvariantCulture, $"\"{edge.SourceId}\" -> \"{edge.TargetId}\" [ id=\"{edge.Id}\" style=\"{style}\" edgetooltip=\"{tooltip}\" labeltooltip=\"{tooltip}\" color=\"{edgeColor}\" fontsize=8 fontname=\"Consolas\" label=\"{label}\" ];");

                lines.Add(activity);
            }

            lines.Add("}");

            return NewLineHelper.JoinLines(lines);
        }

        // A text between the double quotes of a Dot string. A double quote in the text would end the string, and a
        // backslash would start an escape (a backslash at the end would take the closing quote into the string). Each gets
        // a backslash before it, the backslash first. A line end stays as it is: a Dot string can hold one, and the labels
        // have had them from the start.
        private static string Escape(string? text) =>
            (text ?? string.Empty)
                .Replace(@"\", @"\\", StringComparison.Ordinal)
                .Replace(@"""", @"\""", StringComparison.Ordinal);
    }
}
