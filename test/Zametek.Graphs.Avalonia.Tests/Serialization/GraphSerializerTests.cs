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

        // A double quote ends a string of Dot, and a backslash starts an escape. A name with one of them gave a file that
        // Graphviz cannot read, and a name could add attributes of its own to the file. Each is now written with a
        // backslash before it. A line end in a label stays as it is, because a string of Dot can have one.
        [Fact]
        public void GraphViz_escapes_a_quote_and_a_backslash_in_each_text()
        {
            var diagram = new DiagramGraphModel
            {
                Nodes =
                [
                    new DiagramNodeModel
                    {
                        Id = 0,
                        Text = NewLineHelper.JoinLines(@"|a ""b"" c\|", @"|0|"),
                        Name = @"node ""one"" in C:\plans\",
                        BorderColorHexCode = @"#000000",
                    },
                    new DiagramNodeModel { Id = 1, Text = @"|1|", Name = @"plain", BorderColorHexCode = @"#000000" },
                ],
                Edges =
                [
                    new DiagramEdgeModel
                    {
                        Id = 0,
                        SourceId = 0,
                        TargetId = 1,
                        Name = @"edge "" color=""red",
                        Label = @"C:\ ""x""",
                        ShowLabel = true,
                        ForegroundColorHexCode = @"#000000",
                    },
                ],
            };

            string graphViz = Encoding.UTF8.GetString(new GraphSerializer().BuildGraphVizData(diagram));

            graphViz.ShouldContain(@"label=""|a \""b\"" c\\|" + NewLineHelper.NewLine + @"|0|"" ");
            graphViz.ShouldContain(@"tooltip=""node \""one\"" in C:\\plans\\"" ");
            graphViz.ShouldContain(@"edgetooltip=""edge \"" color=\""red"" ");
            graphViz.ShouldContain(@"labeltooltip=""edge \"" color=\""red"" ");
            graphViz.ShouldContain(@"label=""C:\\ \""x\"""" ];");

            // No text ends a string early. Without the escapes, the quotes are those that open and close a string. The
            // file has 8 strings for each of the two nodes, 9 for the edge, and one for the direction of the graph. A
            // label can go over two lines, and so the count is of the whole file.
            string bare = graphViz.Replace(@"\\", string.Empty, StringComparison.Ordinal).Replace(@"\""", string.Empty, StringComparison.Ordinal);
            bare.Count(x => x == '"').ShouldBe(2 * ((2 * 8) + 9 + 1));
        }

        [Fact]
        public void GraphViz_writes_a_text_with_no_quote_and_no_backslash_as_before()
        {
            string graphViz = Encoding.UTF8.GetString(new GraphSerializer().BuildGraphVizData(BuildDiagram()));

            graphViz.ShouldContain(@"label=""|0|" + NewLineHelper.NewLine + @"|0|"" ");
            graphViz.ShouldContain(@"label=""1 (5)" + NewLineHelper.NewLine + @"0|2"" ];");
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
