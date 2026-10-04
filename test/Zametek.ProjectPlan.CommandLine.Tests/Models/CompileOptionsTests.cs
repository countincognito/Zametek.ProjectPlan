using CommandLine;
using Shouldly;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for the options of a request to compile a project, as zpp sends them and as the contract names them: each
    /// stands for an option of zpp's, as zpp has it, and every option of zpp's that a server can act on is there.
    /// </summary>
    public class CompileOptionsTests
    {
        // What zpp's options are to the contract. A member of the options, or of an output, stands for the options of zpp's
        // listed with it.
        private static readonly Dictionary<string, string[]> s_Options = new()
        {
            [@"scenario"] = [@"scenario"],
            [@"baseTheme"] = [@"base-theme"],
            [@"metricsFormat"] = [@"metrics-format"],
            [@"compileTimeout"] = [@"compile-timeout"],
            [@"now"] = [@"now"],
            [@"outputs.project"] = [@"output"],
            [@"outputs.scenarioExport"] = [@"export"],
            [@"outputs.ganttChart"] = [@"gantt-directory", @"gantt-format", @"gantt-size"],
            [@"outputs.arrowGraph"] = [@"arrow-directory", @"arrow-format"],
            [@"outputs.vertexGraph"] = [@"vertex-directory", @"vertex-format"],
            [@"outputs.resourceChart"] = [@"resource-directory", @"resource-format", @"resource-size"],
            [@"outputs.earnedValueChart"] = [@"ev-directory", @"ev-format", @"ev-size"],
            [@"outputs.scenarioChart"] = [@"scenario-chart-directory", @"scenario-chart-format", @"scenario-chart-size"],
        };

        // The options of zpp's that a request does not carry: the plan is its own part, a listing is its own endpoint, the
        // log is the server's, and where to run is the client's.
        private static readonly string[] s_NotSent = [@"input", @"import", @"list-scenarios", @"verbose", @"server", @"local", @"api-key-file"];

        private static string Serialize(CompileOptions options)
        {
            return JsonSerializer.Serialize(options, JobJsonHelper.ClientOptions);
        }

        [Fact]
        public void Serialize_Given_EveryOption_Then_WritesEachAsTheContractNamesIt()
        {
            string json = Serialize(new CompileOptions
            {
                Scenario = @"Beta",
                BaseTheme = BaseTheme.Dark,
                MetricsFormat = MetricsExport.Json,
                CompileTimeout = TimeSpan.FromSeconds(3),
                Now = @"2026-10-02T09:00:00+01:00",
                Outputs = new OutputsOptions
                {
                    Project = new ProjectOptions(),
                    ScenarioExport = new ScenarioExportOptions(),
                    GanttChart = new ChartOptions { Format = PlotExport.Png, Width = 800, Height = 600 },
                    ArrowGraph = new GraphOptions { Format = GraphExport.GraphML },
                    VertexGraph = new GraphOptions { Format = GraphExport.Dot },
                    ResourceChart = new ChartOptions { Format = PlotExport.Svg, Width = 640, Height = 480 },
                    EarnedValueChart = new ChartOptions { Format = PlotExport.Webp, Width = 320, Height = 240 },
                    ScenarioChart = new ChartOptions { Format = PlotExport.Bmp, Width = 100, Height = 50 },
                },
            });

            json.ShouldBe(
                """{"scenario":"Beta","baseTheme":"dark","metricsFormat":"json","compileTimeout":"PT3S","now":"2026-10-02T09:00:00+01:00","outputs":{"project":{},"scenarioExport":{},"ganttChart":{"format":"png","width":800,"height":600},"arrowGraph":{"format":"graphml"},"vertexGraph":{"format":"dot"},"resourceChart":{"format":"svg","width":640,"height":480},"earnedValueChart":{"format":"webp","width":320,"height":240},"scenarioChart":{"format":"bmp","width":100,"height":50}}}""");
        }

        [Fact]
        public void Serialize_Given_NoOptions_Then_WritesNothingForTheServerToDefault()
        {
            Serialize(new CompileOptions()).ShouldBe(@"{}");
        }

        [Fact]
        public void Serialize_Given_AChartOrGraphInItsDefaultFormat_Then_LeavesTheFormatOut()
        {
            Serialize(new CompileOptions
            {
                Outputs = new OutputsOptions
                {
                    GanttChart = new ChartOptions { Width = 800, Height = 600 },
                    ArrowGraph = new GraphOptions(),
                },
            }).ShouldBe(@"{""outputs"":{""ganttChart"":{""width"":800,""height"":600},""arrowGraph"":{}}}");
        }

        [Fact]
        public void Serialize_Given_OutputsThatAreNotAskedFor_Then_LeavesThemOut()
        {
            string json = Serialize(new CompileOptions { Outputs = new OutputsOptions { Project = new ProjectOptions() } });

            json.ShouldBe(@"{""outputs"":{""project"":{}}}");
        }

        [Fact]
        public void Defaults_Given_TheServerDefaultsToThem_Then_TheyAreTheDefaultOfEachType()
        {
            // What a request leaves out is written as the type's default, so the server's defaults must be those of the
            // types, or a value that is not the server's default would be left out too.
            var options = new CompileOptions();

            options.BaseTheme.ShouldBe(default);
            options.MetricsFormat.ShouldBe(default);
            options.BaseTheme.ShouldBe(BaseTheme.Light);
            options.MetricsFormat.ShouldBe(MetricsExport.Markdown);
            new ChartOptions { Width = 1, Height = 1 }.Format.ShouldBe(default);
            new ChartOptions { Width = 1, Height = 1 }.Format.ShouldBe(PlotExport.Jpeg);
            new GraphOptions().Format.ShouldBe(default);
            new GraphOptions().Format.ShouldBe(GraphExport.Jpeg);
        }

        [Fact]
        public void CompileOptions_Given_EachMember_Then_NamedAfterTheOptionOfZppsItStandsFor()
        {
            string[] longNames = LongNames();

            foreach ((string member, string[] zppOptions) in s_Options)
            {
                zppOptions.ShouldNotBeEmpty(member);

                foreach (string zppOption in zppOptions)
                {
                    longNames.ShouldContain(zppOption, $@"{member} stands for --{zppOption}");
                }
            }
        }

        [Fact]
        public void CompileOptions_Given_EveryOptionOfZpps_Then_ARequestCarriesItOrItIsNotOneAServerActsOn()
        {
            // So that an option added to zpp is not left out of the run on a server without anyone deciding to.
            string[] carried = [.. s_Options.Values.SelectMany(x => x)];

            string[] unaccounted = [.. LongNames().Except(carried).Except(s_NotSent)];

            unaccounted.ShouldBeEmpty();
            s_NotSent.Except(LongNames()).ShouldBeEmpty();
            carried.Distinct().Count().ShouldBe(carried.Length);
        }

        [Fact]
        public void CompileOptions_Given_EveryMember_Then_AMemberOfTheMapAboveAndNothingElse()
        {
            // The members of the options, and of the outputs, are the keys of the map.
            string[] members =
            [
                .. typeof(CompileOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Select(x => ApiNamingPolicy.Instance.ConvertName(x.Name))
                    .Where(x => x != @"outputs"),
                .. typeof(OutputsOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Select(x => $@"outputs.{ApiNamingPolicy.Instance.ConvertName(x.Name)}"),
            ];

            members.ShouldBe([.. s_Options.Keys], ignoreOrder: true);
        }

        [Fact]
        public void ChartOptions_Given_ItsMembers_Then_TheFormatAndTheSizeThatZppsOptionsGive()
        {
            typeof(ChartOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(x => x.Name)
                .ShouldBe([nameof(ChartOptions.Format), nameof(ChartOptions.Width), nameof(ChartOptions.Height)]);
            typeof(GraphOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(x => x.Name)
                .ShouldBe([nameof(GraphOptions.Format)]);
        }

        private static string[] LongNames()
        {
            return [.. typeof(Options).GetProperties()
                .Select(x => x.GetCustomAttribute<OptionAttribute>()?.LongName)
                .OfType<string>()];
        }

        [Fact]
        public void Serialize_Given_ZppsOptions_Then_NamesTheOutputsInWordsAsTheContractDoes()
        {
            // The names the contract gives are words, in camelCase: ganttChart and earnedValueChart, not gantt and ev.
            string json = Serialize(CompileOptionsHelper.FromOptions(new Options
            {
                InputFilename = @"plan.zpp",
                GanttDirectory = @"charts",
                GanttSize = [1, 1],
                EVDirectory = @"charts",
                EVSize = [1, 1],
            }));

            json.ShouldContain(@"""ganttChart""");
            json.ShouldContain(@"""earnedValueChart""");
            json.ShouldNotContain(@"""gantt""");
            json.ShouldNotContain(@"""ev""");
            Encoding.UTF8.GetByteCount(json).ShouldBeLessThan(CompileOptionsHelper.MaxOptionsBytes);
        }
    }
}
