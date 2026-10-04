using Shouldly;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;
using Zametek.Engine.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for zpp serve's API when a request is answered: what a request to compile a project is answered with - its
    /// metrics and outputs, the console when it asks for it, the zip when it accepts one - a project's scenarios, what the
    /// server says about itself, and its health. What it answers with when it cannot answer, ProjectProblemsTests pins; and
    /// what a job prints and produces - the same as zpp - ProjectEndpointsParityTests.
    /// </summary>
    public class ProjectEndpointsTests
        : IClassFixture<EngineFixture>, IAsyncLifetime
    {
        private const string c_Now = @"2026-10-02T09:00:00+01:00";

        // Every metric of the answer, named as the contract names it, in the order it writes them.
        private static readonly string[] s_MetricNames =
        [
            @"activityRisk", @"activityRiskWithStandardDeviationCorrection", @"criticalityRisk", @"fibonacciRisk",
            @"geometricActivityRisk", @"geometricCriticalityRisk", @"geometricFibonacciRisk", @"networkCyclomaticComplexity",
            @"networkDuration", @"networkDurationManMonths", @"projectFinishDays", @"projectFinishDate", @"effortEfficiency",
            @"activityEffort", @"directEffort", @"indirectEffort", @"otherEffort", @"totalEffort", @"directCost",
            @"indirectCost", @"otherCost", @"totalCost", @"directBilling", @"indirectBilling", @"otherBilling",
            @"totalBilling", @"directMargin", @"indirectMargin", @"otherMargin", @"totalMargin", @"directMarginAbsolute",
            @"indirectMarginAbsolute", @"otherMarginAbsolute", @"totalMarginAbsolute",
        ];

        private static readonly ServeLimits s_Limits = new();

        private readonly EngineFixture m_Engine;
        private RunningServer? m_Server;

        public ProjectEndpointsTests(EngineFixture engine)
        {
            m_Engine = engine;
        }

        private RunningServer Server => m_Server ?? throw new InvalidOperationException();

        public async Task InitializeAsync()
        {
            m_Server = await RunningServer.StartAsync(m_Engine.JobRunner, new ServeSettings { Limits = s_Limits });
        }

        public async Task DisposeAsync()
        {
            if (m_Server is not null)
            {
                await m_Server.DisposeAsync();
            }
        }

        private static byte[] TwoScenarios()
        {
            return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, @"Assets", @"two-scenarios.zpp"));
        }

        private async Task<HttpResponseMessage> PostAsync(
            CompileOptions? options = null,
            string query = @"")
        {
            using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp", options);
            return await Server.Client.PostAsync($@"/v1/projects/compile{query}", content);
        }

        private async Task<HttpResponseMessage> PostAsync(
            string optionsJson,
            string query = @"")
        {
            using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp");
            content.Add(new StringContent(optionsJson, Encoding.UTF8, @"application/json"), @"options");
            return await Server.Client.PostAsync($@"/v1/projects/compile{query}", content);
        }

        [Fact]
        public async Task Compile_Given_NoOptions_Then_AnswersWithTheMetricsAndNoOutputs()
        {
            using HttpResponseMessage response = await PostAsync();

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe(@"application/json");

            CompileResponse compiled = await RunningServer.ReadAsync<CompileResponse>(response);
            compiled.Outputs.ShouldBeEmpty();
            compiled.Console.ShouldBeNull();
            compiled.Metrics.NetworkDuration.ShouldBe(5);

            // The project finishes five days after its start, 1 January 2024, as the days and as the date.
            compiled.Metrics.ProjectFinishDays.ShouldBe(5);
            compiled.Metrics.ProjectFinishDate.ShouldBe(new DateOnly(2024, 1, 6));
        }

        [Fact]
        public async Task Compile_Given_AnAnswer_Then_NamesEveryMetricAsTheContractDoesInCompactJson()
        {
            using HttpResponseMessage response = await PostAsync();
            string json = await response.Content.ReadAsStringAsync();

            using JsonDocument document = JsonDocument.Parse(json);
            document.RootElement.EnumerateObject().Select(x => x.Name).ShouldBe([@"metrics", @"outputs"]);
            document.RootElement.GetProperty(@"metrics").EnumerateObject().Select(x => x.Name).ShouldBe(s_MetricNames);
            document.RootElement.GetProperty(@"metrics").GetProperty(@"projectFinishDate").GetString().ShouldBe(@"2024-01-06");
            document.RootElement.GetProperty(@"outputs").GetArrayLength().ShouldBe(0);

            // Compact, and not a display of the server's: no line end, no indent.
            json.ShouldNotContain('\n');
            json.ShouldNotContain(@"  ");
        }

        [Fact]
        public async Task Compile_Given_ThatMetricsHaveNoValue_Then_WritesThemAsNull()
        {
            // The finish of a plan that takes no time has no days and no date: known, and none.
            byte[] plan = PlanOfNoDuration();
            using var content = RunningServer.CompileContent(plan, @"nothing.zpp");
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/projects/compile", content);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            JsonElement metrics = document.RootElement.GetProperty(@"metrics");
            metrics.GetProperty(@"projectFinishDays").ValueKind.ShouldBe(JsonValueKind.Null);
            metrics.GetProperty(@"projectFinishDate").ValueKind.ShouldBe(JsonValueKind.Null);
        }

        [Fact]
        public async Task Compile_Given_NoConsoleAsked_Then_NoneIsInTheAnswerWhateverTheMetricsFormat()
        {
            using HttpResponseMessage response = await PostAsync(new CompileOptions { MetricsFormat = MetricsExport.Json });

            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            document.RootElement.TryGetProperty(@"console", out _).ShouldBeFalse();
        }

        [Fact]
        public async Task Compile_Given_TheConsoleIsAsked_Then_AnswersWithWhatZppWouldHavePrintedAndExitedWith()
        {
            using HttpResponseMessage response = await PostAsync(query: @"?include=console");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            ConsoleResponse console = (await RunningServer.ReadAsync<CompileResponse>(response)).Console.ShouldNotBeNull();
            console.ExitCode.ShouldBe(0);
            console.StandardOutput.ShouldStartWith(NewLineHelper.NewLine + @"| ");
            console.StandardError.ShouldBeEmpty();
        }

        [Fact]
        public async Task Compile_Given_TheConsoleIsAskedForTwice_Then_AnswersOnce()
        {
            using HttpResponseMessage response = await PostAsync(query: @"?include=console,console");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await RunningServer.ReadAsync<CompileResponse>(response)).Console.ShouldNotBeNull();
        }

        [Fact]
        public async Task Compile_Given_OutputsAndTheConsole_Then_TheTranscriptHasEachOutputThenTheMetricsBlock()
        {
            using HttpResponseMessage response = await PostAsync(
                new CompileOptions
                {
                    Outputs = new OutputsOptions
                    {
                        Project = new ProjectOptions(),
                        GanttChart = new ChartOptions { Format = PlotExport.Svg, Width = 800, Height = 600 },
                    },
                },
                @"?include=console");

            CompileResponse compiled = await RunningServer.ReadAsync<CompileResponse>(response);
            compiled.Outputs.Select(x => x.Kind).ShouldBe([JobOutput.Project, JobOutput.GanttChart]);
            compiled.Outputs.Select(x => x.FileName).ShouldBe([@"two-scenarios.zpp", @"two-scenarios-gantt.svg"]);
            compiled.Outputs.ShouldAllBe(x => x.Content != null && x.Content.Length > 0);

            // The block, as the standard output has it after the blank line that sets it apart.
            ConsoleResponse console = compiled.Console.ShouldNotBeNull();
            string metrics = console.StandardOutput[NewLineHelper.NewLine.Length..^NewLineHelper.NewLine.Length];
            console.Transcript.ShouldBe(
            [
                new JobTranscriptEntry { Kind = JobTranscriptKind.Output, Index = 0 },
                new JobTranscriptEntry { Kind = JobTranscriptKind.Output, Index = 1 },
                new JobTranscriptEntry { Kind = JobTranscriptKind.Display, Text = metrics },
            ]);
        }

        [Fact]
        public async Task Compile_Given_JsonMetricsAndTheConsole_Then_TheTranscriptHasTheirLine()
        {
            using HttpResponseMessage response = await PostAsync(new CompileOptions { MetricsFormat = MetricsExport.Json }, @"?include=console");

            ConsoleResponse console = (await RunningServer.ReadAsync<CompileResponse>(response)).Console.ShouldNotBeNull();
            console.Transcript.ShouldHaveSingleItem().ShouldBe(new JobTranscriptEntry
            {
                Kind = JobTranscriptKind.Line,
                Text = console.StandardOutput[..^NewLineHelper.NewLine.Length],
            });
        }

        [Fact]
        public async Task Compile_Given_AFormatInItsExactSpelling_Then_TakesIt()
        {
            using HttpResponseMessage response = await PostAsync(@"{""outputs"":{""ganttChart"":{""format"":""png"",""width"":80,""height"":60}}}");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            CompileResponse compiled = await RunningServer.ReadAsync<CompileResponse>(response);
            OutputResponse output = compiled.Outputs.ShouldHaveSingleItem();
            output.Kind.ShouldBe(JobOutput.GanttChart);
            output.FileName.ShouldBe(@"two-scenarios-gantt.png");
            output.ContentType.ShouldBe(@"image/png");
        }

        [Fact]
        public async Task Compile_Given_EveryKindOfOutput_Then_AnswersWithEachInTheOrderTheJobProducesThem()
        {
            using HttpResponseMessage response = await PostAsync(
                new CompileOptions
                {
                    Outputs = new OutputsOptions
                    {
                        ScenarioChart = new ChartOptions { Format = PlotExport.Svg, Width = 100, Height = 100 },
                        EarnedValueChart = new ChartOptions { Format = PlotExport.Svg, Width = 100, Height = 100 },
                        ResourceChart = new ChartOptions { Format = PlotExport.Svg, Width = 100, Height = 100 },
                        VertexGraph = new GraphOptions { Format = GraphExport.GraphML },
                        ArrowGraph = new GraphOptions { Format = GraphExport.Dot },
                        GanttChart = new ChartOptions { Format = PlotExport.Svg, Width = 100, Height = 100 },
                        ScenarioExport = new ScenarioExportOptions(),
                        Project = new ProjectOptions(),
                    },
                });

            CompileResponse compiled = await RunningServer.ReadAsync<CompileResponse>(response);
            compiled.Outputs.Select(x => x.Kind).ShouldBe(Enum.GetValues<JobOutput>());
            compiled.Outputs.Select(x => x.ContentType).ShouldBe(
            [
                @"application/json",
                @"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                @"image/svg+xml",
                @"text/vnd.graphviz",
                @"application/graphml+xml",
                @"image/svg+xml",
                @"image/svg+xml",
                @"image/svg+xml",
            ]);
        }

        [Fact]
        public async Task Compile_Given_AFileNameWithAPath_Then_NamesTheOutputsAfterTheFileAlone()
        {
            // A path that came with the name says nothing about where the server is, and is not in what it answers.
            using var content = RunningServer.CompileContent(
                TwoScenarios(),
                @"some/where/else/two-scenarios.zpp",
                new CompileOptions
                {
                    Outputs = new OutputsOptions
                    {
                        Project = new ProjectOptions(),
                        GanttChart = new ChartOptions { Format = PlotExport.Svg, Width = 100, Height = 100 },
                    },
                });
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/projects/compile", content);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await RunningServer.ReadAsync<CompileResponse>(response)).Outputs.Select(x => x.FileName)
                .ShouldBe([@"two-scenarios.zpp", @"two-scenarios-gantt.svg"]);
        }

        [Fact]
        public async Task Compile_Given_OptionsInAFile_Then_TakesThem()
        {
            using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp");
            content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(@"{""scenario"":""Beta""}")), @"options", @"options.json");
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/projects/compile?include=console", content);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await RunningServer.ReadAsync<CompileResponse>(response)).Console.ShouldNotBeNull().ExitCode.ShouldBe(0);
        }

        [Fact]
        public async Task Compile_Given_TheLongestCompileTimeoutTheLimitAllows_Then_RunsTheJob()
        {
            string longest = IsoDurationHelper.ToString(TimeSpan.FromMilliseconds(s_Limits.MaxCompileTimeoutMilliseconds));

            using HttpResponseMessage response = await PostAsync($@"{{""compileTimeout"":""{longest}""}}");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Compile_Given_AScenarioAndAnImport_Then_RunsIt()
        {
            // The workbook the scenario exports to, imported as a project's Base scenario.
            using HttpResponseMessage exported = await PostAsync(new CompileOptions { Outputs = new OutputsOptions { ScenarioExport = new ScenarioExportOptions() } });
            byte[] workbook = (await RunningServer.ReadAsync<CompileResponse>(exported)).Outputs.ShouldHaveSingleItem().Content.ShouldNotBeNull();

            using var content = RunningServer.CompileContent(workbook, @"plan.xlsx", new CompileOptions { Now = c_Now }, @"import");
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/projects/compile", content);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await RunningServer.ReadAsync<CompileResponse>(response)).Metrics.NetworkDuration.ShouldBe(5);
        }

        [Theory]
        [InlineData(@"plan.xlsx")]
        [InlineData(@"PLAN.XLSX")]
        [InlineData(@"Plan.Xlsx")]
        public async Task Compile_Given_AWorkbookWhateverTheCaseOfItsExtension_Then_ImportsIt(string filename)
        {
            using HttpResponseMessage exported = await PostAsync(new CompileOptions { Outputs = new OutputsOptions { ScenarioExport = new ScenarioExportOptions() } });
            byte[] workbook = (await RunningServer.ReadAsync<CompileResponse>(exported)).Outputs.ShouldHaveSingleItem().Content.ShouldNotBeNull();

            using var content = RunningServer.CompileContent(workbook, filename, null, @"import");
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/projects/compile", content);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Compile_Given_AZipIsAccepted_Then_AnswersWithTheOutputsAndTheRestInResultJson()
        {
            var options = new CompileOptions
            {
                Now = c_Now,
                Outputs = new OutputsOptions
                {
                    Project = new ProjectOptions(),
                    GanttChart = new ChartOptions { Format = PlotExport.Svg, Width = 800, Height = 600 },
                },
            };

            using HttpResponseMessage json = await PostAsync(options, @"?include=console");
            CompileResponse expected = await RunningServer.ReadAsync<CompileResponse>(json);

            using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp", options);
            using var request = new HttpRequestMessage(HttpMethod.Post, @"/v1/projects/compile?include=console") { Content = content };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(@"application/zip"));
            using HttpResponseMessage response = await Server.Client.SendAsync(request);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe(@"application/zip");
            response.Content.Headers.ContentDisposition.ShouldNotBeNull().FileName.ShouldNotBeNull().Trim('"').ShouldBe(@"two-scenarios.zip");

            using var zip = new ZipArchive(await response.Content.ReadAsStreamAsync(), ZipArchiveMode.Read);
            zip.Entries.Select(x => x.FullName).ShouldBe([@"two-scenarios.zpp", @"two-scenarios-gantt.svg", @"result.json"]);

            // Each output as the JSON response carries it, and every file stamped with the time the job ran at.
            foreach (OutputResponse output in expected.Outputs)
            {
                ZipArchiveEntry entry = zip.GetEntry(output.FileName).ShouldNotBeNull();
                using var bytes = new MemoryStream();
                await using (Stream stream = entry.Open())
                {
                    await stream.CopyToAsync(bytes);
                }
                bytes.ToArray().ShouldBe(output.Content);
            }

            DateTimeOffset now = DateTimeOffset.Parse(c_Now, CultureInfo.InvariantCulture);
            zip.Entries.ShouldAllBe(x => x.LastWriteTime.DateTime == now.DateTime);

            ZipArchiveEntry resultEntry = zip.GetEntry(@"result.json").ShouldNotBeNull();
            CompileResponse result;
            await using (Stream stream = resultEntry.Open())
            {
                result = (await JsonSerializer.DeserializeAsync<CompileResponse>(stream, ProjectEndpoints.JsonOptions)).ShouldNotBeNull();
            }

            result.Metrics.ShouldBe(expected.Metrics);
            result.Outputs.Select(x => (x.Kind, x.FileName, x.ContentType)).ShouldBe(expected.Outputs.Select(x => (x.Kind, x.FileName, x.ContentType)));
            result.Outputs.ShouldAllBe(x => x.Content == null);
            ConsoleResponse console = result.Console.ShouldNotBeNull();
            ConsoleResponse expectedConsole = expected.Console.ShouldNotBeNull();
            console.ExitCode.ShouldBe(expectedConsole.ExitCode);
            console.StandardOutput.ShouldBe(expectedConsole.StandardOutput);
            console.StandardError.ShouldBe(expectedConsole.StandardError);
            console.Transcript.ShouldBe(expectedConsole.Transcript);
        }

        [Fact]
        public async Task Compile_Given_ThatAZipIsAcceptedButNotAsked_Then_AnswersWithJson()
        {
            using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp");
            using var request = new HttpRequestMessage(HttpMethod.Post, @"/v1/projects/compile") { Content = content };
            request.Headers.Accept.ParseAdd(@"application/zip;q=0.5, application/json");
            using HttpResponseMessage response = await Server.Client.SendAsync(request);

            response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe(@"application/json");
        }

        [Fact]
        public async Task Compile_Given_AnAnswerThatCouldBeEitherJsonOrAZip_Then_SaysItVariesWithAccept()
        {
            using HttpResponseMessage response = await PostAsync();

            response.Headers.Vary.ShouldContain(@"Accept");
        }

        [Fact]
        public async Task ListScenarios_Given_AProject_Then_ListsThem()
        {
            using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp");
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/projects/scenarios", content);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            string json = await response.Content.ReadAsStringAsync();
            using JsonDocument document = JsonDocument.Parse(json);
            document.RootElement.EnumerateObject().Select(x => x.Name).ShouldBe([@"scenarios"]);
            document.RootElement.GetProperty(@"scenarios")[0].EnumerateObject().Select(x => x.Name).ShouldBe([@"path", @"id", @"isTracked", @"isCurrent"]);

            ScenariosResponse scenarios = JsonSerializer.Deserialize<ScenariosResponse>(json, ProjectEndpoints.JsonOptions).ShouldNotBeNull();
            scenarios.Scenarios.Select(x => (x.Path, x.IsTracked, x.IsCurrent)).ShouldBe([(@"Alpha", true, true), (@"Beta", false, false)]);
            scenarios.Console.ShouldBeNull();
        }

        [Fact]
        public async Task ListScenarios_Given_TheConsoleIsAsked_Then_TheTranscriptHasTheTable()
        {
            using var content = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp");
            using HttpResponseMessage response = await Server.Client.PostAsync(@"/v1/projects/scenarios?include=console", content);

            ConsoleResponse console = (await RunningServer.ReadAsync<ScenariosResponse>(response)).Console.ShouldNotBeNull();
            console.ExitCode.ShouldBe(0);
            console.Transcript.ShouldHaveSingleItem().ShouldBe(new JobTranscriptEntry
            {
                Kind = JobTranscriptKind.Display,
                Text = console.StandardOutput[NewLineHelper.NewLine.Length..^NewLineHelper.NewLine.Length],
            });
        }

        [Fact]
        public async Task GetInfo_Then_SaysWhatTheServerIs()
        {
            using HttpResponseMessage response = await Server.Client.GetAsync(@"/v1/info");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            InfoResponse info = await RunningServer.ReadAsync<InfoResponse>(response);
            info.Version.ShouldBe(Resource.ProjectPlan.Labels.Label_AppVersion);
            info.Culture.ShouldBe(CultureInfo.CurrentCulture.Name);
            info.TimeZone.ShouldBe(TimeZoneInfo.Local.Id);
            info.Limits.ShouldBe(s_Limits);
        }

        [Fact]
        public async Task Health_Given_AServerThatDoesNotWarmUp_Then_LiveButNeverReady()
        {
            using HttpResponseMessage live = await Server.Client.GetAsync(@"/health/live");
            using HttpResponseMessage ready = await Server.Client.GetAsync(@"/health/ready");

            live.StatusCode.ShouldBe(HttpStatusCode.OK);
            ready.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        }

        [Fact]
        public async Task Compile_Given_AJobWithinSmallerChartLimits_Then_RunsItAndRefusesAnyBigger()
        {
            await using RunningServer server = await RunningServer.StartAsync(
                m_Engine.JobRunner,
                new ServeSettings { Limits = s_Limits with { MaxChartWidth = 300, MaxChartHeight = 200 } });

            using var withinLimits = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp", new CompileOptions
            {
                Outputs = new OutputsOptions { GanttChart = new ChartOptions { Format = PlotExport.Png, Width = 300, Height = 200 } },
            });
            using var tooWide = RunningServer.CompileContent(TwoScenarios(), @"two-scenarios.zpp", new CompileOptions
            {
                Outputs = new OutputsOptions { GanttChart = new ChartOptions { Format = PlotExport.Png, Width = 301, Height = 200 } },
            });

            using HttpResponseMessage ran = await server.Client.PostAsync(@"/v1/projects/compile", withinLimits);
            using HttpResponseMessage refused = await server.Client.PostAsync(@"/v1/projects/compile", tooWide);

            ran.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await RunningServer.ReadAsync<CompileResponse>(ran)).Outputs.ShouldHaveSingleItem().Kind.ShouldBe(JobOutput.GanttChart);
            refused.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        }

        // The project of the two scenarios, in which every activity has no duration.
        private static byte[] PlanOfNoDuration()
        {
            Newtonsoft.Json.Linq.JObject plan = Newtonsoft.Json.Linq.JObject.Parse(Encoding.UTF8.GetString(TwoScenarios()));

            foreach (Newtonsoft.Json.Linq.JToken activity in plan.SelectTokens(@"$.Files[*].Scenario.DependentActivities[*].Activity"))
            {
                activity[@"Duration"] = 0;
            }

            return Encoding.UTF8.GetBytes(plan.ToString());
        }
    }
}
