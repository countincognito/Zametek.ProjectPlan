using Shouldly;
using System.Text;
using Xunit;

namespace Zametek.Graphs.Avalonia.Tests.Serialization
{
    // The GraphML and GraphViz exports end their lines with NewLineHelper.NewLine on every platform, so that the same
    // diagram exports to the same bytes on Windows and on Linux. Windows' own line end is "\r\n", which is where these
    // tests have teeth.
    public class GraphSerializerTests
    {
        [Fact]
        public void GraphML_ends_its_lines_with_LF()
        {
            string graphML = Encoding.UTF8.GetString(new GraphSerializer().BuildGraphMLData(BuildDiagram()));

            ShouldEndLinesWithNewLine(graphML);
        }

        [Fact]
        public void GraphViz_ends_its_lines_with_LF()
        {
            string graphViz = Encoding.UTF8.GetString(new GraphSerializer().BuildGraphVizData(BuildDiagram()));

            ShouldEndLinesWithNewLine(graphViz);
        }

        // The text has lines, and each of them ends with NewLineHelper.NewLine: there is no carriage return anywhere.
        private static void ShouldEndLinesWithNewLine(string text)
        {
            text.ShouldContain(NewLineHelper.NewLine);
            text.ShouldNotContain(NewLineHelper.ClassicMacNewLine);
        }

        // Two nodes joined by an edge, the labels split over two lines as the application's diagram builders split them.
        private static DiagramGraphModel BuildDiagram()
        {
            List<DiagramNodeModel> nodes = [.. Enumerable.Range(0, 2).Select(i => new DiagramNodeModel
            {
                Id = i,
                Text = NewLineHelper.JoinLines($"|{i}|", $"|{i * 11}|"),
                FillColorHexCode = @"#D3D3D3",
                BorderColorHexCode = @"#000000",
                BorderThickness = 1.0,
            })];

            List<DiagramEdgeModel> edges =
            [
                new DiagramEdgeModel
                {
                    Id = 0,
                    SourceId = 0,
                    TargetId = 1,
                    ForegroundColorHexCode = @"#000000",
                    StrokeThickness = 1.0,
                    Label = NewLineHelper.JoinLines(@"1 (5)", @"0|2"),
                    ShowLabel = true,
                },
            ];

            return new DiagramGraphModel { Nodes = nodes, Edges = edges };
        }
    }
}
