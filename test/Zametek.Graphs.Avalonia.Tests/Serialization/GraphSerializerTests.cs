using Shouldly;
using System.Globalization;
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

        // Numbers in both exports are written the same whatever the culture, so the same diagram exports to the same
        // bytes wherever it is exported. A culture that writes a decimal comma is where these tests have teeth.
        [Fact]
        public void GraphML_writes_its_numbers_the_same_in_every_culture()
        {
            string graphML = InDecimalCommaCulture(
                () => Encoding.UTF8.GetString(new GraphSerializer().BuildGraphMLData(BuildDiagram(thickness: 1.5, position: 10.25))));

            graphML.ShouldContain(@"x=""10.25""");
            graphML.ShouldContain(@"width=""1.5""");
            graphML.ShouldNotContain(@",25");
            graphML.ShouldNotContain(@"1,5");
        }

        [Fact]
        public void GraphViz_writes_its_numbers_the_same_in_every_culture()
        {
            string graphViz = InDecimalCommaCulture(
                () => Encoding.UTF8.GetString(new GraphSerializer().BuildGraphVizData(BuildDiagram(thickness: 1.5, position: 10.25))));

            graphViz.ShouldContain(@"penwidth=1.5 ");
            graphViz.ShouldNotContain(@"1,5");
        }

        // The text has lines, and each of them ends with NewLineHelper.NewLine: there is no carriage return anywhere.
        private static void ShouldEndLinesWithNewLine(string text)
        {
            text.ShouldContain(NewLineHelper.NewLine);
            text.ShouldNotContain(NewLineHelper.ClassicMacNewLine);
        }

        // Runs the export in a culture that writes 1.5 as 1,5 - made here rather than taken from the machine, whose
        // cultures depend on how it was installed.
        private static T InDecimalCommaCulture<T>(Func<T> export)
        {
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = @",";
            culture.NumberFormat.NumberGroupSeparator = @".";

            CultureInfo original = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = culture;
            try
            {
                return export();
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        // Two nodes joined by an edge, the labels split over two lines as the application's diagram builders split them.
        private static DiagramGraphModel BuildDiagram(double thickness = 1.0, double position = 0.0)
        {
            List<DiagramNodeModel> nodes = [.. Enumerable.Range(0, 2).Select(i => new DiagramNodeModel
            {
                Id = i,
                X = position,
                Y = position,
                Width = 40.0,
                Height = 40.0,
                Text = NewLineHelper.JoinLines($"|{i}|", $"|{i * 11}|"),
                FillColorHexCode = @"#D3D3D3",
                BorderColorHexCode = @"#000000",
                BorderThickness = thickness,
            })];

            List<DiagramEdgeModel> edges =
            [
                new DiagramEdgeModel
                {
                    Id = 0,
                    SourceId = 0,
                    TargetId = 1,
                    ForegroundColorHexCode = @"#000000",
                    StrokeThickness = thickness,
                    Label = NewLineHelper.JoinLines(@"1 (5)", @"0|2"),
                    ShowLabel = true,
                },
            ];

            return new DiagramGraphModel { Nodes = nodes, Edges = edges };
        }
    }
}
