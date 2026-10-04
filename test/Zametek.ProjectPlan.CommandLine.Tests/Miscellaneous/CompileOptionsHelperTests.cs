using Shouldly;
using System.Reflection;
using System.Text.Json;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for how zpp serve reads the options of a request - every problem with them, not the first - checks them
    /// against its limits, makes of them the engine's request that zpp makes of its own, and names and types the outputs
    /// they ask for; and for how zpp makes a request's options of its own.
    /// </summary>
    public class CompileOptionsHelperTests
    {
        private static readonly ServeLimits s_Limits = new()
        {
            MaxChartWidth = 1000,
            MaxChartHeight = 800,
            MaxCompileTimeoutMilliseconds = 20_000,
        };

        private static (CompileOptions? Options, ProblemCollector Problems) Read(
            string? json,
            bool isImport = false)
        {
            var problems = new ProblemCollector();
            CompileOptions? options = CompileOptionsHelper.Read(json, isImport, s_Limits, problems);
            return (options, problems);
        }

        private static string[] Where(ProblemCollector problems)
        {
            return [.. problems.Errors.Select(x => $@"{x.Pointer} {x.Code}")];
        }

        [Fact]
        public void Read_Given_NoJson_Then_ZppsDefaultsAndNoProblems()
        {
            (CompileOptions? options, ProblemCollector problems) = Read(null);

            options.ShouldBe(new CompileOptions());
            options.ShouldNotBeNull().BaseTheme.ShouldBe(BaseTheme.Light);
            options.MetricsFormat.ShouldBe(MetricsExport.Markdown);
            options.CompileTimeout.ShouldBeNull();
            problems.HasErrors.ShouldBeFalse();
        }

        [Fact]
        public void Read_Given_EmptyOptions_Then_ZppsDefaultsAndNoProblems()
        {
            (CompileOptions? options, ProblemCollector problems) = Read(@"{}");

            options.ShouldBe(new CompileOptions());
            problems.HasErrors.ShouldBeFalse();
        }

        [Fact]
        public void Read_Given_EveryOptionWithinTheLimits_Then_ReadsEachAndNoProblems()
        {
            (CompileOptions? options, ProblemCollector problems) = Read(
                """
                {
                    "scenario": "Beta",
                    "baseTheme": "dark",
                    "metricsFormat": "json",
                    "compileTimeout": "PT20S",
                    "now": "2026-10-02T09:00:00+01:00",
                    "outputs": {
                        "project": {},
                        "scenarioExport": {},
                        "ganttChart": { "format": "png", "width": 1000, "height": 800 },
                        "arrowGraph": { "format": "graphml" },
                        "vertexGraph": { "format": "dot" },
                        "resourceChart": { "format": "svg", "width": 640, "height": 480 },
                        "earnedValueChart": { "format": "webp", "width": 1, "height": 1 },
                        "scenarioChart": { "format": "bmp", "width": 100, "height": 50 }
                    }
                }
                """);

            problems.HasErrors.ShouldBeFalse();
            options.ShouldBe(new CompileOptions
            {
                Scenario = @"Beta",
                BaseTheme = BaseTheme.Dark,
                MetricsFormat = MetricsExport.Json,
                CompileTimeout = TimeSpan.FromSeconds(20),
                Now = @"2026-10-02T09:00:00+01:00",
                Outputs = new OutputsOptions
                {
                    Project = new ProjectOptions(),
                    ScenarioExport = new ScenarioExportOptions(),
                    GanttChart = new ChartOptions { Format = PlotExport.Png, Width = 1000, Height = 800 },
                    ArrowGraph = new GraphOptions { Format = GraphExport.GraphML },
                    VertexGraph = new GraphOptions { Format = GraphExport.Dot },
                    ResourceChart = new ChartOptions { Format = PlotExport.Svg, Width = 640, Height = 480 },
                    EarnedValueChart = new ChartOptions { Format = PlotExport.Webp, Width = 1, Height = 1 },
                    ScenarioChart = new ChartOptions { Format = PlotExport.Bmp, Width = 100, Height = 50 },
                },
            });
        }

        [Fact]
        public void Read_Given_AChartOrGraphWithoutAFormat_Then_ZppsDefaultFormat()
        {
            (CompileOptions? options, _) = Read(@"{""outputs"":{""ganttChart"":{""width"":800,""height"":600},""arrowGraph"":{}}}");

            OutputsOptions outputs = options.ShouldNotBeNull().Outputs.ShouldNotBeNull();
            outputs.GanttChart.ShouldNotBeNull().Format.ShouldBe(PlotExport.Jpeg);
            outputs.ArrowGraph.ShouldNotBeNull().Format.ShouldBe(GraphExport.Jpeg);
        }

        [Fact]
        public void Read_Given_MembersThatAreNull_Then_AsIfTheyWereNotGiven()
        {
            (CompileOptions? options, ProblemCollector problems) = Read(
                @"{""scenario"":null,""baseTheme"":null,""metricsFormat"":null,""compileTimeout"":null,""now"":null,""outputs"":null}");

            problems.HasErrors.ShouldBeFalse();
            options.ShouldBe(new CompileOptions());
        }

        [Fact]
        public void Read_Given_OutputsThatAreNull_Then_NoneAreAskedFor()
        {
            (CompileOptions? options, ProblemCollector problems) = Read(
                @"{""outputs"":{""project"":null,""scenarioExport"":null,""ganttChart"":null,""arrowGraph"":null,""vertexGraph"":null,""resourceChart"":null,""earnedValueChart"":null,""scenarioChart"":null}}");

            problems.HasErrors.ShouldBeFalse();
            options.ShouldNotBeNull().Outputs.ShouldBe(new OutputsOptions());
            CompileOptionsHelper.GetRequestedOutputs(options).ShouldBeEmpty();
        }

        [Fact]
        public void Read_Given_TheLongestAndShortestCompileTimeouts_Then_ReadsThem()
        {
            Read(@"{""compileTimeout"":""PT0.001S""}").Options.ShouldNotBeNull().CompileTimeout.ShouldBe(TimeSpan.FromMilliseconds(1));
            Read(@"{""compileTimeout"":""PT20S""}").Options.ShouldNotBeNull().CompileTimeout.ShouldBe(TimeSpan.FromSeconds(20));
        }

        public static TheoryData<string, bool, string[]> InvalidOptions => new()
        {
            // Not an object.
            { @"[]", false, [@"#/options wrongType"] },
            { @"""x""", false, [@"#/options wrongType"] },
            { @"5", false, [@"#/options wrongType"] },
            { @"true", false, [@"#/options wrongType"] },

            // What is not known: names are matched as they are written.
            { @"{""colour"":""red""}", false, [@"#/options/colour unknownProperty"] },
            { @"{""Scenario"":""Beta""}", false, [@"#/options/Scenario unknownProperty"] },
            { @"{""a/b~c"":1}", false, [@"#/options/a~1b~0c unknownProperty"] },
            { @"{""a b"":1}", false, [@"#/options/a%20b unknownProperty"] },

            // The scenario.
            { @"{""scenario"":5}", false, [@"#/options/scenario wrongType"] },
            { @"{""scenario"":""Beta""}", true, [@"#/options/scenario notAllowedWithImport"] },

            // The enums: the API's names, as they are written, and nothing else.
            { @"{""baseTheme"":""Sepia""}", false, [@"#/options/baseTheme notAllowed"] },
            { @"{""baseTheme"":""Dark""}", false, [@"#/options/baseTheme notAllowed"] },
            { @"{""baseTheme"":1}", false, [@"#/options/baseTheme wrongType"] },
            { @"{""metricsFormat"":""xml""}", false, [@"#/options/metricsFormat notAllowed"] },

            // The compile timeout: an ISO 8601 duration, from a millisecond to the server's limit.
            { @"{""compileTimeout"":5}", false, [@"#/options/compileTimeout wrongType"] },
            { @"{""compileTimeout"":""5s""}", false, [@"#/options/compileTimeout invalidFormat"] },
            { @"{""compileTimeout"":""PT0S""}", false, [@"#/options/compileTimeout outOfRange"] },
            { @"{""compileTimeout"":""PT0.0005S""}", false, [@"#/options/compileTimeout outOfRange"] },
            { @"{""compileTimeout"":""PT20.001S""}", false, [@"#/options/compileTimeout outOfRange"] },
            { @"{""compileTimeout"":""PT1M""}", false, [@"#/options/compileTimeout outOfRange"] },

            // The time the job runs at: with its offset.
            { @"{""now"":5}", false, [@"#/options/now wrongType"] },
            { @"{""now"":""2026-10-02T09:00:00""}", false, [@"#/options/now invalidFormat"] },
            { @"{""now"":""yesterday""}", false, [@"#/options/now invalidFormat"] },

            // The outputs.
            { @"{""outputs"":[]}", false, [@"#/options/outputs wrongType"] },
            { @"{""outputs"":{""ev"":{}}}", false, [@"#/options/outputs/ev unknownProperty"] },
            { @"{""outputs"":{""project"":[]}}", false, [@"#/options/outputs/project wrongType"] },
            { @"{""outputs"":{""project"":{""x"":1}}}", false, [@"#/options/outputs/project/x unknownProperty"] },
            { @"{""outputs"":{""scenarioExport"":true}}", false, [@"#/options/outputs/scenarioExport wrongType"] },
            { @"{""outputs"":{""scenarioExport"":{""x"":1}}}", false, [@"#/options/outputs/scenarioExport/x unknownProperty"] },
            { @"{""outputs"":{""arrowGraph"":5}}", false, [@"#/options/outputs/arrowGraph wrongType"] },
            { @"{""outputs"":{""arrowGraph"":{""size"":5}}}", false, [@"#/options/outputs/arrowGraph/size unknownProperty"] },
            { @"{""outputs"":{""vertexGraph"":{""format"":""tiff""}}}", false, [@"#/options/outputs/vertexGraph/format notAllowed"] },

            // A chart needs its size, within the server's limits.
            { @"{""outputs"":{""ganttChart"":{}}}", false, [@"#/options/outputs/ganttChart/width required", @"#/options/outputs/ganttChart/height required"] },
            { @"{""outputs"":{""ganttChart"":{""width"":null,""height"":null}}}", false, [@"#/options/outputs/ganttChart/width required", @"#/options/outputs/ganttChart/height required"] },
            { @"{""outputs"":{""resourceChart"":{""width"":10}}}", false, [@"#/options/outputs/resourceChart/height required"] },
            { @"{""outputs"":{""ganttChart"":{""width"":0,""height"":801}}}", false, [@"#/options/outputs/ganttChart/width outOfRange", @"#/options/outputs/ganttChart/height outOfRange"] },
            { @"{""outputs"":{""earnedValueChart"":{""width"":1001,""height"":800}}}", false, [@"#/options/outputs/earnedValueChart/width outOfRange"] },
            { @"{""outputs"":{""scenarioChart"":{""width"":1000,""height"":-1}}}", false, [@"#/options/outputs/scenarioChart/height outOfRange"] },
            { @"{""outputs"":{""ganttChart"":{""width"":1.5,""height"":10}}}", false, [@"#/options/outputs/ganttChart/width wrongType"] },
            { @"{""outputs"":{""ganttChart"":{""width"":""10"",""height"":10}}}", false, [@"#/options/outputs/ganttChart/width wrongType"] },
            { @"{""outputs"":{""ganttChart"":{""width"":10,""height"":10,""format"":""tiff""}}}", false, [@"#/options/outputs/ganttChart/format notAllowed"] },
            { @"{""outputs"":{""ganttChart"":{""width"":10,""height"":10,""dpi"":96}}}", false, [@"#/options/outputs/ganttChart/dpi unknownProperty"] },
            { @"{""outputs"":{""ganttChart"":7}}", false, [@"#/options/outputs/ganttChart wrongType"] },

            // Every problem, in the order the members are written.
            {
                @"{""compileTimeout"":""PT0S"",""colour"":""red"",""outputs"":{""ganttChart"":{""width"":0}}}",
                false,
                [
                    @"#/options/compileTimeout outOfRange",
                    @"#/options/colour unknownProperty",
                    @"#/options/outputs/ganttChart/width outOfRange",
                    @"#/options/outputs/ganttChart/height required",
                ]
            },
        };

        [Theory]
        [MemberData(nameof(InvalidOptions))]
        public void Read_Given_OptionsThatAreNotValid_Then_NoOptionsAndEachProblemListedWhereItIs(string json, bool isImport, string[] expected)
        {
            (CompileOptions? options, ProblemCollector problems) = Read(json, isImport);

            options.ShouldBeNull();
            Where(problems).ShouldBe(expected);
            problems.IsMalformed.ShouldBeFalse();
            problems.Errors.ShouldAllBe(x => x.Parameter == null && !string.IsNullOrWhiteSpace(x.Detail));
        }

        [Fact]
        public void Read_Given_ASizeAtTheLimitsAndOnePixelOver_Then_ReadsTheFormerAndRefusesTheLatter()
        {
            Read(@"{""outputs"":{""ganttChart"":{""width"":1000,""height"":800}}}").Options.ShouldNotBeNull();
            Read(@"{""outputs"":{""ganttChart"":{""width"":1,""height"":1}}}").Options.ShouldNotBeNull();
            Read(@"{""outputs"":{""ganttChart"":{""width"":1001,""height"":800}}}").Options.ShouldBeNull();
            Read(@"{""outputs"":{""ganttChart"":{""width"":1000,""height"":801}}}").Options.ShouldBeNull();
        }

        [Fact]
        public void Read_Given_ProblemsWithThePixelsAndTheTimeout_Then_TheDetailsNameTheLimits()
        {
            (_, ProblemCollector problems) = Read(@"{""compileTimeout"":""PT1M"",""outputs"":{""ganttChart"":{""width"":1001,""height"":801}}}");

            problems.Errors.Select(x => x.Detail).ShouldBe(
            [
                string.Format(Resource.ProjectPlan.Messages.Message_ServeErrorDurationOutOfRange, @"PT0.001S", @"PT20S"),
                string.Format(Resource.ProjectPlan.Messages.Message_ServeErrorPixelsOutOfRange, 1000),
                string.Format(Resource.ProjectPlan.Messages.Message_ServeErrorPixelsOutOfRange, 800),
            ]);
        }

        [Fact]
        public void Read_Given_EnumsThatAreWrong_Then_TheDetailListsTheNamesTheAPIGivesThem()
        {
            (_, ProblemCollector problems) = Read(@"{""outputs"":{""arrowGraph"":{""format"":""tiff""}}}");

            problems.Errors.ShouldHaveSingleItem().Detail.ShouldBe(
                string.Format(Resource.ProjectPlan.Messages.Message_ServeErrorMustBeOneOf, @"jpeg, png, pdf, svg, graphml, dot"));
        }

        [Theory]
        [InlineData(@"{")]
        [InlineData(@"")]
        [InlineData(@"not json")]
        [InlineData(@"{""scenario"":}")]
        [InlineData(@"{""scenario"":""Beta""} trailing")]
        public void Read_Given_JsonThatIsNotJson_Then_MalformedWithItsPlace(string json)
        {
            (CompileOptions? options, ProblemCollector problems) = Read(json);

            options.ShouldBeNull();
            problems.IsMalformed.ShouldBeTrue();
            ProblemError error = problems.Errors.ShouldHaveSingleItem();
            error.Pointer.ShouldBe(@"#/options");
            error.Code.ShouldBe(@"invalidFormat");
            error.Detail.ShouldStartWith(@"must be valid JSON (line ");
        }

        [Fact]
        public void Read_Given_JsonNestedTooDeeply_Then_MalformedAndNotAStackOverflow()
        {
            string json = string.Concat(Enumerable.Repeat(@"{""a"":", 40)) + @"1" + new string('}', 40);

            (CompileOptions? options, ProblemCollector problems) = Read(json);

            options.ShouldBeNull();
            problems.IsMalformed.ShouldBeTrue();
        }

        [Fact]
        public void Read_Given_ProblemsAlreadyCollected_Then_AddsToThem()
        {
            var problems = new ProblemCollector();
            problems.AddMalformed(new ProblemError { Parameter = @"include", Code = @"notAllowed", Detail = @"x" });

            CompileOptions? options = CompileOptionsHelper.Read(@"{""colour"":""red""}", isImport: false, s_Limits, problems);

            options.ShouldBeNull();
            problems.Errors.Count.ShouldBe(2);
        }

        [Fact]
        public void Read_Given_ValidOptionsAfterAProblemElsewhere_Then_ReadsThemAnyway()
        {
            // A problem with a parameter is not a problem with the options: they are still read, and still checked.
            var problems = new ProblemCollector();
            problems.AddMalformed(new ProblemError { Parameter = @"include", Code = @"notAllowed", Detail = @"x" });

            CompileOptions? options = CompileOptionsHelper.Read(@"{""scenario"":""Beta""}", isImport: false, s_Limits, problems);

            options.ShouldNotBeNull().Scenario.ShouldBe(@"Beta");
            problems.Errors.Count.ShouldBe(1);
        }

        [Theory]
        [InlineData(@"#/options", @"scenario", @"#/options/scenario")]
        [InlineData(@"#/options", @"a/b", @"#/options/a~1b")]
        [InlineData(@"#/options", @"a~b", @"#/options/a~0b")]
        [InlineData(@"#/options", @"~/", @"#/options/~0~1")]
        [InlineData(@"#/options", @"a b", @"#/options/a%20b")]
        [InlineData(@"#/options", @"é", @"#/options/%C3%A9")]
        [InlineData(@"#/options", @"a#b?c", @"#/options/a%23b%3Fc")]
        [InlineData(@"#", @"project", @"#/project")]
        [InlineData(@"#/options/outputs", @"ganttChart", @"#/options/outputs/ganttChart")]
        public void GetPointer_Given_AName_Then_ThePointerToItAsAUriFragment(string parent, string name, string expected)
        {
            CompileOptionsHelper.GetPointer(parent, name).ShouldBe(expected);
        }

        [Fact]
        public void ToJobRequest_Given_EveryOption_Then_TheRequestZppMakesOfTheSame()
        {
            using var input = new MemoryStream();

            JobRequest request = CompileOptionsHelper.ToJobRequest(
                new CompileOptions
                {
                    Scenario = @"Beta",
                    BaseTheme = BaseTheme.Dark,
                    CompileTimeout = TimeSpan.FromSeconds(3),
                    Now = @"2026-10-02T09:00:00+01:00",
                    Outputs = new OutputsOptions
                    {
                        Project = new ProjectOptions(),
                        ScenarioExport = new ScenarioExportOptions(),
                        GanttChart = new ChartOptions { Format = PlotExport.Png, Width = 800, Height = 600 },
                        ArrowGraph = new GraphOptions { Format = GraphExport.Dot },
                        VertexGraph = new GraphOptions { Format = GraphExport.Pdf },
                        ResourceChart = new ChartOptions { Format = PlotExport.Svg, Width = 640, Height = 480 },
                        EarnedValueChart = new ChartOptions { Format = PlotExport.Webp, Width = 320, Height = 240 },
                        ScenarioChart = new ChartOptions { Format = PlotExport.Bmp, Width = 100, Height = 50 },
                    },
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

            CompileOptionsHelper.ToJobRequest(new CompileOptions(), input, null, new ServeLimits())
                .ShouldBe(new JobRequest { Input = input });
        }

        [Fact]
        public void ToJobRequest_Given_NoCompileTimeoutAndALowerLimit_Then_TheLimit()
        {
            using var input = new MemoryStream();

            CompileOptionsHelper.ToJobRequest(new CompileOptions(), input, null, new ServeLimits { MaxCompileTimeoutMilliseconds = 1500 })
                .CompileTimeoutMilliseconds.ShouldBe(1500);
        }

        [Theory]
        [InlineData(15_000_000, 1500)]
        [InlineData(10_000, 1)]
        [InlineData(29_999, 2)]
        public void ToJobRequest_Given_ACompileTimeout_Then_ItInWholeMilliseconds(long ticks, int expected)
        {
            using var input = new MemoryStream();

            CompileOptionsHelper.ToJobRequest(new CompileOptions { CompileTimeout = TimeSpan.FromTicks(ticks) }, input, null, s_Limits)
                .CompileTimeoutMilliseconds.ShouldBe(expected);
        }

        [Fact]
        public void ToJobRequest_Given_ACompileTimeoutOfLessThanAMillisecond_Then_AMillisecondSoThatItIsNotNone()
        {
            using var input = new MemoryStream();

            CompileOptionsHelper.ToJobRequest(new CompileOptions { CompileTimeout = TimeSpan.FromTicks(1) }, input, null, s_Limits)
                .CompileTimeoutMilliseconds.ShouldBe(1);
        }

        [Fact]
        public void ToJobRequest_Given_OnlyTheProject_Then_AsksForNoCharts()
        {
            using var input = new MemoryStream();

            JobRequest request = CompileOptionsHelper.ToJobRequest(
                new CompileOptions { Outputs = new OutputsOptions { Project = new ProjectOptions() } },
                input,
                null,
                s_Limits);

            request.SaveProject.ShouldBeTrue();
            request.ExportFormat.ShouldBeNull();
            request.GanttChart.ShouldBeNull();
            request.ArrowGraph.ShouldBeNull();
            request.VertexGraph.ShouldBeNull();
            request.ResourceChart.ShouldBeNull();
            request.EarnedValueChart.ShouldBeNull();
            request.ScenarioChart.ShouldBeNull();
        }

        [Fact]
        public void GetRequestedOutputs_Given_EveryOutput_Then_EachInTheOrderTheJobProducesThem()
        {
            var options = new CompileOptions
            {
                Outputs = new OutputsOptions
                {
                    ScenarioChart = new ChartOptions { Width = 1, Height = 1 },
                    EarnedValueChart = new ChartOptions { Width = 1, Height = 1 },
                    ResourceChart = new ChartOptions { Width = 1, Height = 1 },
                    VertexGraph = new GraphOptions(),
                    ArrowGraph = new GraphOptions(),
                    GanttChart = new ChartOptions { Width = 1, Height = 1 },
                    ScenarioExport = new ScenarioExportOptions(),
                    Project = new ProjectOptions(),
                },
            };

            CompileOptionsHelper.GetRequestedOutputs(options).ShouldBe(Enum.GetValues<JobOutput>());
        }

        [Fact]
        public void GetRequestedOutputs_Given_NoOutputs_Then_None()
        {
            CompileOptionsHelper.GetRequestedOutputs(new CompileOptions()).ShouldBeEmpty();
            CompileOptionsHelper.GetRequestedOutputs(new CompileOptions { Outputs = new OutputsOptions() }).ShouldBeEmpty();
        }

        [Fact]
        public void GetRequestedOutputs_Given_SomeOutputs_Then_JustThose()
        {
            var options = new CompileOptions
            {
                Outputs = new OutputsOptions
                {
                    VertexGraph = new GraphOptions(),
                    Project = new ProjectOptions(),
                    EarnedValueChart = new ChartOptions { Width = 1, Height = 1 },
                },
            };

            CompileOptionsHelper.GetRequestedOutputs(options).ShouldBe([JobOutput.Project, JobOutput.VertexGraph, JobOutput.EarnedValueChart]);
        }

        [Fact]
        public void GetOutputName_Given_EachOutput_Then_ItsMemberOfTheOutputsOptionsAndOfTheAnswer()
        {
            string[] names = [.. Enum.GetValues<JobOutput>().Select(CompileOptionsHelper.GetOutputName)];

            names.ShouldBe(
            [
                @"project", @"scenarioExport", @"ganttChart", @"arrowGraph", @"vertexGraph", @"resourceChart", @"earnedValueChart", @"scenarioChart",
            ]);

            // Each is a member of the outputs a request asks for, and each member is one of them.
            string[] members = [.. typeof(OutputsOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(x => ApiNamingPolicy.Instance.ConvertName(x.Name))];

            members.ShouldBe(names);
        }

        [Theory]
        [InlineData(JobOutput.Project, @"plan.zpp")]
        [InlineData(JobOutput.ScenarioExport, @"plan.xlsx")]
        public void BuildOutputFilename_Given_TheProjectOrItsExport_Then_NamedAfterThePlan(JobOutput output, string expected)
        {
            CompileOptionsHelper.BuildOutputFilename(output, new CompileOptions(), @"plan").ShouldBe(expected);
        }

        [Fact]
        public void BuildOutputFilename_Given_EachChartAndGraph_Then_TheNameZppGivesItsFile()
        {
            var options = new CompileOptions
            {
                Outputs = new OutputsOptions
                {
                    GanttChart = new ChartOptions { Format = PlotExport.Png, Width = 1, Height = 1 },
                    ArrowGraph = new GraphOptions { Format = GraphExport.GraphML },
                    VertexGraph = new GraphOptions { Format = GraphExport.Dot },
                    ResourceChart = new ChartOptions { Format = PlotExport.Webp, Width = 1, Height = 1 },
                    EarnedValueChart = new ChartOptions { Format = PlotExport.Svg, Width = 1, Height = 1 },
                    ScenarioChart = new ChartOptions { Format = PlotExport.Bmp, Width = 1, Height = 1 },
                },
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
                CompileOptionsHelper.BuildOutputFilename(output, options, @"plan")
                    .ShouldBe(Path.GetFileName(Program.BuildExportFilePath(@"charts", @"plan", suffix, format)));
            }
        }

        [Fact]
        public void BuildOutputFilename_Given_AChartOrGraphNotAskedFor_Then_NamedAsTheDefaultFormatIs()
        {
            CompileOptionsHelper.BuildOutputFilename(JobOutput.GanttChart, new CompileOptions(), @"plan")
                .ShouldBe(Path.GetFileName(Program.BuildExportFilePath(@"charts", @"plan", Resource.ProjectPlan.Suffixes.Suffix_GanttChart, PlotExport.Jpeg.GetDescription())));
            CompileOptionsHelper.BuildOutputFilename(JobOutput.ArrowGraph, new CompileOptions(), @"plan")
                .ShouldBe(Path.GetFileName(Program.BuildExportFilePath(@"graphs", @"plan", Resource.ProjectPlan.Suffixes.Suffix_ArrowChart, GraphExport.Jpeg.GetDescription())));
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
        [InlineData(@"plan-ARROW.DOT", @"text/vnd.graphviz")]
        [InlineData(@"plan.unknown", @"application/octet-stream")]
        [InlineData(@"plan", @"application/octet-stream")]
        public void GetContentType_Given_AnOutputsFile_Then_ItsMediaType(string filename, string contentType)
        {
            CompileOptionsHelper.GetContentType(filename).ShouldBe(contentType);
        }

        [Fact]
        public void FromOptions_Given_EveryOptionARequestTakes_Then_TheSameLessThePaths()
        {
            var options = new Options
            {
                InputFilename = @"plan.zpp",
                Scenario = @"Beta",
                OutputFilename = @"saved.zpp",
                ExportFilename = @"exported.xlsx",
                BaseTheme = BaseTheme.Dark,
                MetricsFormat = MetricsExport.Json,
                CompileTimeoutMilliseconds = 3000,
                Now = @"2026-10-02T09:00:00+01:00",
                GanttDirectory = @"charts",
                GanttFormat = PlotExport.Png,
                GanttSize = [800, 600],
                ArrowGraphDirectory = @"graphs",
                ArrowGraphFormat = GraphExport.GraphML,
                VertexGraphDirectory = @"graphs",
                VertexGraphFormat = GraphExport.Dot,
                ResourceDirectory = @"charts",
                ResourceFormat = PlotExport.Svg,
                ResourceSize = [640, 480],
                EVDirectory = @"charts",
                EVFormat = PlotExport.Webp,
                EVSize = [320, 240],
                ScenarioChartDirectory = @"charts",
                ScenarioChartFormat = PlotExport.Bmp,
                ScenarioChartSize = [100, 50],
            };

            CompileOptionsHelper.FromOptions(options).ShouldBe(new CompileOptions
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
        }

        [Fact]
        public void FromOptions_Given_NoOutputs_Then_AsksForNoneWithZppsCompileTimeout()
        {
            CompileOptionsHelper.FromOptions(new Options { InputFilename = @"plan.zpp" })
                .ShouldBe(new CompileOptions { CompileTimeout = TimeSpan.FromMilliseconds(AppSettingsModel.DefaultCompilationTimeoutMilliseconds) });
        }

        [Fact]
        public void FromOptions_Given_OnlyTheProject_Then_AsksForItAlone()
        {
            CompileOptionsHelper.FromOptions(new Options { InputFilename = @"plan.zpp", OutputFilename = @"saved.zpp" }).Outputs
                .ShouldBe(new OutputsOptions { Project = new ProjectOptions() });
        }

        [Fact]
        public void FromOptions_Given_OptionsThatAreSent_Then_ReadBackAsTheyWere()
        {
            // What zpp sends is what the server reads: through JSON, as the client writes it, to Read as the server reads it.
            var options = new Options
            {
                InputFilename = @"plan.zpp",
                Scenario = @"Beta",
                OutputFilename = @"saved.zpp",
                ExportFilename = @"exported.xlsx",
                BaseTheme = BaseTheme.Dark,
                MetricsFormat = MetricsExport.Table,
                CompileTimeoutMilliseconds = 3500,
                Now = @"2026-10-02T09:00:00+01:00",
                GanttDirectory = @"charts",
                GanttFormat = PlotExport.Webp,
                GanttSize = [800, 600],
                ArrowGraphDirectory = @"graphs",
                ArrowGraphFormat = GraphExport.GraphML,
                EVDirectory = @"charts",
                EVFormat = PlotExport.Svg,
                EVSize = [320, 240],
            };

            CompileOptions sent = CompileOptionsHelper.FromOptions(options);
            string json = JsonSerializer.Serialize(sent, JobJsonHelper.ClientOptions);

            (CompileOptions? read, ProblemCollector problems) = Read(json);

            problems.HasErrors.ShouldBeFalse();
            read.ShouldBe(sent);
        }

        [Fact]
        public void FromOptions_Given_DefaultOptionsThatAreSent_Then_ReadBackAsTheyWere()
        {
            CompileOptions sent = CompileOptionsHelper.FromOptions(new Options { InputFilename = @"plan.zpp" });
            string json = JsonSerializer.Serialize(sent, JobJsonHelper.ClientOptions);

            (CompileOptions? read, ProblemCollector problems) = Read(json);

            problems.HasErrors.ShouldBeFalse();
            read.ShouldBe(sent);
        }
    }
}
