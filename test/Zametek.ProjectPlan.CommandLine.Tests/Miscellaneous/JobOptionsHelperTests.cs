using Shouldly;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for how zpp serve checks a job's options against its limits, the
    /// engine's request it makes of them - the one zpp makes of its own - and
    /// the names and media types it gives the job's outputs.
    /// </summary>
    public class JobOptionsHelperTests
    {
        private static readonly ServeLimits s_Limits = new()
        {
            MaxChartWidth = 1000,
            MaxChartHeight = 800,
            MaxCompileTimeoutMilliseconds = 20_000,
        };

        [Fact]
        public void Validate_Given_OptionsWithinTheLimits_Then_Null()
        {
            JobOptionsHelper.Validate(
                new JobOptions
                {
                    Scenario = @"Beta",
                    CompileTimeout = 20_000,
                    Now = @"2026-10-02T09:00:00Z",
                    Gantt = new ChartOptions { Width = 1000, Height = 800 },
                    EV = new ChartOptions { Width = 1, Height = 1 },
                },
                isImport: false,
                s_Limits).ShouldBeNull();
        }

        [Fact]
        public void Validate_Given_AScenarioWithAnImport_Then_Why()
        {
            JobOptionsHelper.Validate(new JobOptions { Scenario = @"Beta" }, isImport: true, s_Limits)
                .ShouldBe(Resource.ProjectPlan.Messages.Message_ServeScenarioOnlyWithInput);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(20_001)]
        public void Validate_Given_ACompileTimeoutOutsideTheLimit_Then_Why(int compileTimeout)
        {
            JobOptionsHelper.Validate(new JobOptions { CompileTimeout = compileTimeout }, isImport: false, s_Limits)
                .ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeCompileTimeoutOutOfRange, 20_000));
        }

        [Fact]
        public void Validate_Given_NowWithoutAnOffset_Then_Why()
        {
            JobOptionsHelper.Validate(new JobOptions { Now = @"2026-10-02T09:00:00" }, isImport: false, s_Limits)
                .ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_OptionMustBeADateTimeWithOffset, @"'now'"));
        }

        [Theory]
        [InlineData(@"gantt")]
        [InlineData(@"resource")]
        [InlineData(@"ev")]
        [InlineData(@"scenarioChart")]
        public void Validate_Given_AChartTooLarge_Then_WhyNamingTheChart(string name)
        {
            var chart = new ChartOptions { Width = 1001, Height = 800 };
            JobOptions options = name switch
            {
                @"gantt" => new JobOptions { Gantt = chart },
                @"resource" => new JobOptions { Resource = chart },
                @"ev" => new JobOptions { EV = chart },
                _ => new JobOptions { ScenarioChart = chart },
            };

            JobOptionsHelper.Validate(options, isImport: false, s_Limits)
                .ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ServeChartSizeOutOfRange, name, 1000, 800));
        }

        [Theory]
        [InlineData(0, 800)]
        [InlineData(1000, 0)]
        [InlineData(1000, 801)]
        public void Validate_Given_AChartOfAnySizeOutsideTheLimits_Then_Why(int width, int height)
        {
            JobOptionsHelper.Validate(new JobOptions { Gantt = new ChartOptions { Width = width, Height = height } }, isImport: false, s_Limits)
                .ShouldNotBeNull();
        }

        [Fact]
        public void ToJobRequest_Given_EveryOption_Then_TheRequestZppMakesOfTheSame()
        {
            using var input = new MemoryStream();

            JobRequest request = JobOptionsHelper.ToJobRequest(
                new JobOptions
                {
                    Scenario = @"Beta",
                    Output = true,
                    Export = true,
                    BaseTheme = BaseTheme.Dark,
                    CompileTimeout = 3000,
                    Now = @"2026-10-02T09:00:00+01:00",
                    Gantt = new ChartOptions { Format = PlotExport.Png, Width = 800, Height = 600 },
                    Arrow = new GraphOptions { Format = GraphExport.Dot },
                    Vertex = new GraphOptions { Format = GraphExport.Pdf },
                    Resource = new ChartOptions { Format = PlotExport.Svg, Width = 640, Height = 480 },
                    EV = new ChartOptions { Format = PlotExport.Webp, Width = 320, Height = 240 },
                    ScenarioChart = new ChartOptions { Format = PlotExport.Bmp, Width = 100, Height = 50 },
                },
                input,
                ProjectScenarioImportFormat.Xlsx,
                s_Limits);

            request.ShouldBe(new JobRequest
            {
                Input = input,
                ImportFormat = ProjectScenarioImportFormat.Xlsx,
                Scenario = @"Beta",
                BaseTheme = BaseTheme.Dark,
                CompileTimeoutMilliseconds = 3000,
                Now = new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.FromHours(1)),
                SaveProject = true,
                ExportFormat = ProjectScenarioExportFormat.Xlsx,
                GanttChart = new ChartOutputRequest(ChartImageFormat.Png, 800, 600),
                ArrowGraph = new GraphOutputRequest(GraphExportFormat.GraphViz),
                VertexGraph = new GraphOutputRequest(GraphExportFormat.Pdf),
                ResourceChart = new ChartOutputRequest(ChartImageFormat.Svg, 640, 480),
                EarnedValueChart = new ChartOutputRequest(ChartImageFormat.Webp, 320, 240),
                ScenarioChart = new ChartOutputRequest(ChartImageFormat.Bmp, 100, 50),
            });
        }

        [Fact]
        public void ToJobRequest_Given_NoOptions_Then_ZppsDefaults()
        {
            using var input = new MemoryStream();

            JobOptionsHelper.ToJobRequest(new JobOptions(), input, null, new ServeLimits())
                .ShouldBe(new JobRequest { Input = input });
        }

        [Fact]
        public void ToJobRequest_Given_NoCompileTimeoutAndALowerLimit_Then_TheLimit()
        {
            using var input = new MemoryStream();

            JobOptionsHelper.ToJobRequest(new JobOptions(), input, null, new ServeLimits { MaxCompileTimeoutMilliseconds = 1500 })
                .CompileTimeoutMilliseconds.ShouldBe(1500);
        }

        [Theory]
        [InlineData(JobOutput.Project, @"plan.zpp")]
        [InlineData(JobOutput.ScenarioExport, @"plan.xlsx")]
        public void BuildOutputFilename_Given_TheProjectOrItsExport_Then_NamedAfterThePlan(JobOutput output, string expected)
        {
            JobOptionsHelper.BuildOutputFilename(output, new JobOptions(), @"plan").ShouldBe(expected);
        }

        [Fact]
        public void BuildOutputFilename_Given_EachChartAndGraph_Then_TheNameZppGivesItsFile()
        {
            var options = new JobOptions
            {
                Gantt = new ChartOptions { Format = PlotExport.Png, Width = 1, Height = 1 },
                Arrow = new GraphOptions { Format = GraphExport.GraphML },
                Vertex = new GraphOptions { Format = GraphExport.Dot },
                Resource = new ChartOptions { Format = PlotExport.Webp, Width = 1, Height = 1 },
                EV = new ChartOptions { Format = PlotExport.Svg, Width = 1, Height = 1 },
                ScenarioChart = new ChartOptions { Format = PlotExport.Bmp, Width = 1, Height = 1 },
            };

            (JobOutput Output, string Suffix, string Format)[] expected =
            [
                (JobOutput.GanttChart, Resource.ProjectPlan.Suffixes.Suffix_GanttChart, PlotExport.Png.GetDescription()),
                (JobOutput.ArrowGraph, Resource.ProjectPlan.Suffixes.Suffix_ArrowChart, GraphExport.GraphML.GetDescription()),
                (JobOutput.VertexGraph, Resource.ProjectPlan.Suffixes.Suffix_VertexChart, GraphExport.Dot.GetDescription()),
                (JobOutput.ResourceChart, Resource.ProjectPlan.Suffixes.Suffix_ResourceChart, PlotExport.Webp.GetDescription()),
                (JobOutput.EarnedValueChart, Resource.ProjectPlan.Suffixes.Suffix_EarnedValueChart, PlotExport.Svg.GetDescription()),
                (JobOutput.ScenarioChart, Resource.ProjectPlan.Suffixes.Suffix_ScenarioChart, PlotExport.Bmp.GetDescription()),
            ];

            foreach ((JobOutput output, string suffix, string format) in expected)
            {
                JobOptionsHelper.BuildOutputFilename(output, options, @"plan")
                    .ShouldBe(Path.GetFileName(Program.BuildExportFilePath(@"charts", @"plan", suffix, format)));
            }
        }

        [Theory]
        [InlineData(@"plan.zpp", @"application/json")]
        [InlineData(@"plan.xlsx", @"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
        [InlineData(@"plan-gantt.jpeg", @"image/jpeg")]
        [InlineData(@"plan-gantt.png", @"image/png")]
        [InlineData(@"plan-gantt.bmp", @"image/bmp")]
        [InlineData(@"plan-gantt.webp", @"image/webp")]
        [InlineData(@"plan-gantt.svg", @"image/svg+xml")]
        [InlineData(@"plan-arrow.pdf", @"application/pdf")]
        [InlineData(@"plan-arrow.graphml", @"application/graphml+xml")]
        [InlineData(@"plan-arrow.dot", @"text/vnd.graphviz")]
        [InlineData(@"plan.unknown", @"application/octet-stream")]
        public void GetContentType_Given_AnOutputsFile_Then_ItsMediaType(string filename, string contentType)
        {
            JobOptionsHelper.GetContentType(filename).ShouldBe(contentType);
        }
    }
}
