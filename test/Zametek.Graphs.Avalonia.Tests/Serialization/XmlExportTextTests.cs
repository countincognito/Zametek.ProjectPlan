using Shouldly;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace Zametek.Graphs.Avalonia.Tests.Serialization
{
    // The fixed-layout SVG and the GraphML export are XML. A label can have a character that XML 1.0 does not permit, for
    // example U+000B, because a project file can give one with an escape of JSON. The SVG export then failed with a message
    // of the XML library, and the GraphML export wrote &#xB;, which a reader of XML refuses. The ids of GraphML were also
    // written in the current culture, and the background of the SVG had no namespace (from the review of src, test and pkg).
    public class XmlExportTextTests
    {
        private static readonly XNamespace s_Svg = @"http://www.w3.org/2000/svg";

        [Theory]
        [InlineData("plain", "plain")]
        [InlineData("a\vb", "ab")]
        [InlineData("\u0001\u0008\u000B\u000C\u000E\u001F\uFFFE\uFFFF", "")]
        [InlineData("tab\tline\nreturn\r", "tab\tline\nreturn\r")]
        [InlineData("smile \U0001F600 here", "smile \U0001F600 here")]
        [InlineData("", "")]
        public void RemoveNotPermitted_keeps_each_character_that_XML_permits_and_no_other(string text, string expected)
        {
            XmlCharacters.RemoveNotPermitted(text).ShouldBe(expected);
        }

        // In code and not in the data of a theory: xunit gives a surrogate alone to a theory as U+FFFD.
        [Fact]
        public void RemoveNotPermitted_removes_a_surrogate_that_is_not_part_of_a_pair()
        {
            XmlCharacters.RemoveNotPermitted(string.Concat(@"half ", "\uD83D", @" pair")).ShouldBe(@"half  pair");
            XmlCharacters.RemoveNotPermitted(string.Concat(@"end ", "\uDE00")).ShouldBe(@"end ");
            XmlCharacters.RemoveNotPermitted(string.Concat("\uDE00", "\uD83D")).ShouldBe(string.Empty);
        }

        [Fact]
        public void RemoveNotPermitted_gives_an_empty_text_for_no_text()
        {
            XmlCharacters.RemoveNotPermitted(null).ShouldBe(string.Empty);
        }

        [Fact]
        public void GraphML_with_a_character_that_XML_does_not_permit_is_a_file_that_a_reader_reads()
        {
            string graphML = Encoding.UTF8.GetString(new GraphSerializer().BuildGraphMLData(BuildDiagram("one\vtwo", "edge\u0001")));

            XDocument document = XDocument.Parse(graphML);
            List<string> texts = [.. document.Descendants().Where(x => x.Name.LocalName is @"NodeLabel" or @"EdgeLabel").Select(x => x.Value)];
            texts.ShouldContain(@"onetwo");
            texts.ShouldContain(@"edge");
        }

        [Fact]
        public void Svg_with_a_character_that_XML_does_not_permit_is_written_without_it()
        {
            var engine = new MsaglGraphLayoutEngine();

            string svg = Encoding.UTF8.GetString(engine.RenderSvg(BuildDiagram("one\vtwo", "edge"), GraphConfigurations.Vertex, GraphTheme.Light));

            XDocument document = XDocument.Parse(svg);
            document.Descendants(s_Svg + @"tspan").Select(x => x.Value).ShouldContain(@"onetwo");
        }

        [Theory]
        [InlineData(GraphTheme.Light)]
        [InlineData(GraphTheme.Dark)]
        public void Svg_draws_its_background_as_an_element_of_SVG(GraphTheme theme)
        {
            var engine = new MsaglGraphLayoutEngine();

            string svg = Encoding.UTF8.GetString(engine.RenderSvg(BuildDiagram("one", "edge"), GraphConfigurations.Arrow, theme));

            svg.ShouldNotContain(@"xmlns=""""");
            XElement background = XDocument.Parse(svg).Root.ShouldNotBeNull().Elements().First();
            background.Name.ShouldBe(s_Svg + @"rect");
            background.Attribute(@"fill-opacity").ShouldNotBeNull().Value.ShouldBe(@"1.0");
        }

        [Fact]
        public void GraphML_writes_a_negative_id_with_the_minus_sign_of_the_invariant_culture()
        {
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NegativeSign = "\u2212";
            CultureInfo original = CultureInfo.CurrentCulture;
            string graphML;

            try
            {
                CultureInfo.CurrentCulture = culture;
                graphML = Encoding.UTF8.GetString(new GraphSerializer().BuildGraphMLData(BuildDiagram("one", "edge", firstId: -1)));
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }

            graphML.ShouldContain(@"id=""n-1""");
            graphML.ShouldContain(@"id=""e-1""");
            graphML.ShouldContain(@"source=""n-1""");
            graphML.ShouldNotContain("\u2212");
        }

        // Two nodes joined by one edge.
        private static DiagramGraphModel BuildDiagram(string nodeText, string edgeLabel, int firstId = 0)
        {
            List<DiagramNodeModel> nodes = [.. Enumerable.Range(firstId, 2).Select(i => new DiagramNodeModel
            {
                Id = i,
                Width = 40.0,
                Height = 40.0,
                Text = i == firstId ? nodeText : @"other",
                FillColorHexCode = @"#D3D3D3",
                BorderColorHexCode = @"#000000",
                BorderThickness = 1.0,
            })];

            List<DiagramEdgeModel> edges =
            [
                new DiagramEdgeModel
                {
                    Id = firstId,
                    SourceId = firstId,
                    TargetId = firstId + 1,
                    ForegroundColorHexCode = @"#000000",
                    StrokeThickness = 1.0,
                    Label = edgeLabel,
                    ShowLabel = true,
                },
            ];

            return new DiagramGraphModel { Nodes = nodes, Edges = edges };
        }
    }
}
