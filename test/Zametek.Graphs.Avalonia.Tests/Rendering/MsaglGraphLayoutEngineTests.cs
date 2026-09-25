using Shouldly;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Zametek.Graphs.Avalonia.Tests.Rendering
{
    // Guards the determinism of the MSAGL layout engine. MSAGL's Drawing.Graph keeps its nodes in a
    // Hashtable keyed by the node id string, and .NET randomises string hashing per process, so anything
    // that follows MSAGL's own enumeration order changes from one run of the application to the next. The
    // hash seed cannot be varied inside a test process, but the keys can: renaming every node (same diagram
    // order, same edges, same labels) reshuffles the Hashtable just as a new process would, so an engine that
    // depends only on the diagram's order must produce exactly the same output for both. Several renamings
    // are used because any single one may, in a given process, happen to leave an order-dependent layout
    // unchanged; a regression has to survive all of them at once to go unnoticed.
    public class MsaglGraphLayoutEngineTests
    {
        private const int c_NodeCount = 40;

        private static readonly int[] s_RenamedIdOffsets = [1000, 2000, 3000, 5000, 10000, 123457];

        [Theory]
        [InlineData(nameof(GraphConfigurations.Arrow))]
        [InlineData(nameof(GraphConfigurations.Vertex))]
        public void Svg_does_not_depend_on_the_node_ids(string configurationName)
        {
            GraphConfiguration configuration = ConfigurationNamed(configurationName);
            var engine = new MsaglGraphLayoutEngine();

            string original = Encoding.UTF8.GetString(engine.RenderSvg(BuildDiagram(idOffset: 0), configuration, GraphTheme.Light));

            foreach (int idOffset in s_RenamedIdOffsets)
            {
                string renamed = Encoding.UTF8.GetString(engine.RenderSvg(BuildDiagram(idOffset), configuration, GraphTheme.Light));
                renamed.ShouldBe(original, $@"ids renamed by +{idOffset}");
            }
        }

        [Theory]
        [InlineData(nameof(GraphConfigurations.Arrow))]
        [InlineData(nameof(GraphConfigurations.Vertex))]
        public void Layout_does_not_depend_on_the_node_ids(string configurationName)
        {
            GraphConfiguration configuration = ConfigurationNamed(configurationName);
            var engine = new MsaglGraphLayoutEngine();

            GraphLayoutModel original = engine.BuildLayout(BuildDiagram(idOffset: 0), configuration, GraphTheme.Light);
            original.Nodes.Select(x => x.Id).ShouldBe(Enumerable.Range(0, c_NodeCount));

            foreach (int idOffset in s_RenamedIdOffsets)
            {
                GraphLayoutModel renamed = engine.BuildLayout(BuildDiagram(idOffset), configuration, GraphTheme.Light);

                // The nodes come back in the diagram's order...
                renamed.Nodes.Select(x => x.Id).ShouldBe(Enumerable.Range(idOffset, c_NodeCount), $@"ids renamed by +{idOffset}");

                // ...and in the same places, node for node.
                renamed.Nodes.Select(x => (x.X, x.Y)).ShouldBe(original.Nodes.Select(x => (x.X, x.Y)), $@"ids renamed by +{idOffset}");
                renamed.Width.ShouldBe(original.Width, $@"ids renamed by +{idOffset}");
                renamed.Height.ShouldBe(original.Height, $@"ids renamed by +{idOffset}");
            }
        }

        [Fact]
        public void Svg_writes_the_nodes_in_the_diagram_order()
        {
            var engine = new MsaglGraphLayoutEngine();

            string svg = Encoding.UTF8.GetString(engine.RenderSvg(BuildDiagram(idOffset: 0), GraphConfigurations.Vertex, GraphTheme.Light));

            List<string> nodeLabels = [.. Regex.Matches(svg, @">(N\d{2})</tspan>").Select(x => x.Groups[1].Value)];
            nodeLabels.ShouldBe(Enumerable.Range(0, c_NodeCount).Select(NodeLabel));
        }

        [Fact]
        public void Rendering_leaves_the_current_culture_as_it_was()
        {
            var engine = new MsaglGraphLayoutEngine();
            CultureInfo callerCulture = CultureInfo.CurrentCulture;

            try
            {
                // A culture whose decimal separator is a comma, so a leak in either direction shows.
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(@"de-DE");

                string svg = Encoding.UTF8.GetString(engine.RenderSvg(BuildDiagram(idOffset: 0), GraphConfigurations.Arrow, GraphTheme.Light));

                CultureInfo.CurrentCulture.Name.ShouldBe(@"de-DE");
                Regex.IsMatch(svg, @"=""-?\d+,\d").ShouldBeFalse(@"SVG numbers must be written with the invariant culture");
            }
            finally
            {
                CultureInfo.CurrentCulture = callerCulture;
            }
        }

        private static GraphConfiguration ConfigurationNamed(string name) => name switch
        {
            nameof(GraphConfigurations.Arrow) => GraphConfigurations.Arrow,
            nameof(GraphConfigurations.Vertex) => GraphConfigurations.Vertex,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };

        private static string NodeLabel(int index) => $@"N{index:D2}";

        // A layered, acyclic graph big enough for MSAGL's ordering heuristics to be sensitive to the order
        // they are fed: a spine that keeps it connected plus forward cross links. Built from a fixed seed,
        // so both calls produce the same structure and labels; only the node ids differ. No two edges join
        // the same pair of nodes: the application's graphs never have such parallel edges (none of the sample
        // projects produce one), and MSAGL's Spline routing - the vertex preset - lays parallel edges out
        // slightly differently each time even within one process, which would test MSAGL rather than the
        // engine.
        private static DiagramGraphModel BuildDiagram(int idOffset)
        {
            var random = new Random(20260925);

            List<DiagramNodeModel> nodes = [.. Enumerable.Range(0, c_NodeCount).Select(i => new DiagramNodeModel
            {
                Id = idOffset + i,
                Text = NodeLabel(i),
                FillColorHexCode = @"#D3D3D3",
                BorderColorHexCode = @"#000000",
                BorderThickness = 1.0,
            })];

            var links = new List<(int Source, int Target)>();

            void Link(int source, int target)
            {
                if (!links.Contains((source, target)))
                {
                    links.Add((source, target));
                }
            }

            for (int target = 1; target < c_NodeCount; target++)
            {
                Link(random.Next(Math.Max(0, target - 6), target), target);
            }

            for (int i = 0; i < 25; i++)
            {
                int source = random.Next(0, c_NodeCount - 1);
                Link(source, random.Next(source + 1, Math.Min(c_NodeCount, source + 8)));
            }

            List<DiagramEdgeModel> edges = [.. links.Select((link, index) => new DiagramEdgeModel
            {
                Id = index,
                SourceId = idOffset + link.Source,
                TargetId = idOffset + link.Target,
                ForegroundColorHexCode = @"#000000",
                StrokeThickness = 1.0,
                Label = $@"E{index:D2}",
                ShowLabel = true,
            })];

            return new DiagramGraphModel { Nodes = nodes, Edges = edges };
        }
    }
}
