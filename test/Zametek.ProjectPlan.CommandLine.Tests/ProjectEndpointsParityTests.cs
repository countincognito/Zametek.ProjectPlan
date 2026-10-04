using Shouldly;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// zpp serve's promise: a request sent to it ends as zpp would have ended it, given the same options as flags - the
    /// same exit code, the same text on stdout and stderr, and the same files, byte for byte, under the same names - and
    /// its console, which the request asked for, replayed through zpp's own console, prints exactly what zpp printed. The
    /// status says how it ended: 200 when zpp would have exited with 0, a problem when it would not. Each test runs zpp's
    /// Main and the server on the same plan and compares the two. In the same collection as ProgramExitCodeTests, because
    /// Main swaps the console's streams while it runs.
    /// </summary>
    [Collection(ProgramExitCodeTests.CollectionName)]
    public class ProjectEndpointsParityTests
        : IClassFixture<EngineFixture>, IAsyncLifetime
    {
        private const string c_Now = @"2026-10-02T09:00:00+01:00";

        // A line zpp's log wrote on stderr. The server writes its jobs' log to its own log instead.
        private static readonly Regex s_LogLine = new(@"^\[\d\d:\d\d:\d\d [A-Z]{3}\] .*(\r?\n|$)", RegexOptions.Multiline);

        private readonly EngineFixture m_Engine;
        private readonly string m_TempDirectory;
        private RunningServer? m_Server;

        public ProjectEndpointsParityTests(EngineFixture engine)
        {
            m_Engine = engine;
            m_TempDirectory = Path.Combine(Path.GetTempPath(), $@"zpp-parity-{Guid.NewGuid():N}");
            Directory.CreateDirectory(m_TempDirectory);
        }

        public async Task InitializeAsync()
        {
            m_Server = await RunningServer.StartAsync(m_Engine.JobRunner);
        }

        public async Task DisposeAsync()
        {
            if (m_Server is not null)
            {
                await m_Server.DisposeAsync();
            }

            try
            {
                Directory.Delete(m_TempDirectory, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup only.
            }
        }

        private static string AssetPath(string filename)
        {
            return Path.Combine(AppContext.BaseDirectory, @"Assets", filename);
        }

        // The plan zpp serve warms up on, in a file of its own.
        private string SamplePath()
        {
            string path = Path.Combine(m_TempDirectory, @"sample.zpp");

            if (!File.Exists(path))
            {
                using Stream sample = WarmUpService.OpenSample();
                using FileStream file = File.Create(path);
                sample.CopyTo(file);
            }

            return path;
        }

        [Fact]
        public async Task Compile_Given_EveryOutputInRasterFormats_Then_DoesWhatZppDoes()
        {
            await ShouldDoWhatZppDoesAsync(SamplePath(), new CompileOptions
            {
                MetricsFormat = MetricsExport.Json,
                Now = c_Now,
                Outputs = new OutputsOptions
                {
                    Project = new ProjectOptions(),
                    ScenarioExport = new ScenarioExportOptions(),
                    GanttChart = new ChartOptions { Format = PlotExport.Png, Width = 800, Height = 600 },
                    ArrowGraph = new GraphOptions { Format = GraphExport.Pdf },
                    VertexGraph = new GraphOptions { Format = GraphExport.Png },
                    ResourceChart = new ChartOptions { Format = PlotExport.Jpeg, Width = 640, Height = 480 },
                    EarnedValueChart = new ChartOptions { Format = PlotExport.Bmp, Width = 320, Height = 240 },
                    ScenarioChart = new ChartOptions { Format = PlotExport.Webp, Width = 500, Height = 400 },
                },
            });
        }

        [Fact]
        public async Task Compile_Given_EveryOutputInVectorAndDataFormats_Then_DoesWhatZppDoes()
        {
            await ShouldDoWhatZppDoesAsync(SamplePath(), new CompileOptions
            {
                BaseTheme = BaseTheme.Dark,
                MetricsFormat = MetricsExport.Markdown,
                Now = c_Now,
                Outputs = new OutputsOptions
                {
                    GanttChart = new ChartOptions { Format = PlotExport.Svg, Width = 800, Height = 600 },
                    ArrowGraph = new GraphOptions { Format = GraphExport.GraphML },
                    VertexGraph = new GraphOptions { Format = GraphExport.Dot },
                    ResourceChart = new ChartOptions { Format = PlotExport.Svg, Width = 800, Height = 600 },
                    EarnedValueChart = new ChartOptions { Format = PlotExport.Svg, Width = 800, Height = 600 },
                    ScenarioChart = new ChartOptions { Format = PlotExport.Svg, Width = 800, Height = 600 },
                },
            });
        }

        [Fact]
        public async Task Compile_Given_TableMetricsAndJpegGraphs_Then_DoesWhatZppDoes()
        {
            await ShouldDoWhatZppDoesAsync(SamplePath(), new CompileOptions
            {
                MetricsFormat = MetricsExport.Table,
                CompileTimeout = TimeSpan.FromSeconds(30),
                Outputs = new OutputsOptions
                {
                    ArrowGraph = new GraphOptions { Format = GraphExport.Jpeg },
                    VertexGraph = new GraphOptions { Format = GraphExport.Svg },
                },
            });
        }

        [Fact]
        public async Task Compile_Given_AScenarioByName_Then_DoesWhatZppDoes()
        {
            await ShouldDoWhatZppDoesAsync(AssetPath(@"two-scenarios.zpp"), new CompileOptions
            {
                Scenario = @"Beta",
                Now = c_Now,
                Outputs = new OutputsOptions
                {
                    Project = new ProjectOptions(),
                    GanttChart = new ChartOptions { Format = PlotExport.Svg, Width = 800, Height = 600 },
                },
            });
        }

        [Fact]
        public async Task Compile_Given_AScenarioThatIsNotThere_Then_DoesWhatZppDoes()
        {
            await ShouldDoWhatZppDoesAsync(
                AssetPath(@"two-scenarios.zpp"),
                new CompileOptions { Scenario = @"Gamma" },
                HttpStatusCode.UnprocessableEntity);
        }

        [Fact]
        public async Task Compile_Given_APlanThatDoesNotCompile_Then_DoesWhatZppDoes()
        {
            await ShouldDoWhatZppDoesAsync(
                AssetPath(@"broken-dependency.zpp"),
                new CompileOptions
                {
                    Outputs = new OutputsOptions
                    {
                        Project = new ProjectOptions(),
                        GanttChart = new ChartOptions { Format = PlotExport.Png, Width = 800, Height = 600 },
                    },
                },
                HttpStatusCode.UnprocessableEntity);
        }

        [Fact]
        public async Task Compile_Given_AFileThatIsNotAProject_Then_DoesWhatZppDoes()
        {
            string plan = Path.Combine(m_TempDirectory, @"not-a-plan.zpp");
            await File.WriteAllTextAsync(plan, @"This is not a plan.");

            await ShouldDoWhatZppDoesAsync(plan, new CompileOptions(), HttpStatusCode.UnprocessableEntity);
        }

        [Fact]
        public async Task Compile_Given_AWorkbookToImport_Then_DoesWhatZppDoes()
        {
            // The workbook zpp exports from the sample, at a fixed time, so that both import the same bytes.
            string workbook = Path.Combine(m_TempDirectory, @"plan.xlsx");
            (await RunZppAsync(@"-i", SamplePath(), @"-x", workbook, @"--now", c_Now)).ExitCode.ShouldBe(0);

            // Not the project: an import gives the plan's scenario a new id each time.
            await ShouldDoWhatZppDoesAsync(
                workbook,
                new CompileOptions
                {
                    MetricsFormat = MetricsExport.Json,
                    Now = c_Now,
                    Outputs = new OutputsOptions
                    {
                        ScenarioExport = new ScenarioExportOptions(),
                        GanttChart = new ChartOptions { Format = PlotExport.Png, Width = 800, Height = 600 },
                        VertexGraph = new GraphOptions { Format = GraphExport.Svg },
                    },
                },
                isImport: true);
        }

        [Fact]
        public async Task Compile_Given_AFileThatIsNotAWorkbook_Then_DoesWhatZppDoes()
        {
            string workbook = Path.Combine(m_TempDirectory, @"not-a-workbook.xlsx");
            await File.WriteAllTextAsync(workbook, @"This is not a workbook.");

            await ShouldDoWhatZppDoesAsync(workbook, new CompileOptions(), HttpStatusCode.UnprocessableEntity, isImport: true);
        }

        [Fact]
        public async Task ListScenarios_Given_AProject_Then_PrintsWhatZppPrints()
        {
            string asset = AssetPath(@"two-scenarios.zpp");
            (int exitCode, string output, string error) = await RunZppAsync(@"-i", asset, @"--list-scenarios");

            using var content = RunningServer.CompileContent(File.ReadAllBytes(asset), Path.GetFileName(asset));
            using HttpResponseMessage response = await m_Server!.Client.PostAsync(@"/v1/projects/scenarios?include=console", content);
            response.EnsureSuccessStatusCode();
            ScenariosResponse scenarios = await RunningServer.ReadAsync<ScenariosResponse>(response);
            ConsoleResponse console = scenarios.Console.ShouldNotBeNull();

            console.ExitCode.ShouldBe(exitCode);
            console.StandardOutput.ShouldBe(output);
            console.StandardError.ShouldBe(WithoutLogLines(error));
            scenarios.Scenarios.Select(x => x.Path).ShouldBe([@"Alpha", @"Beta"]);

            (string replayedOutput, string replayedError) = await ReplayAsync(console.Transcript);
            replayedOutput.ShouldBe(output);
            replayedError.ShouldBe(s_LogLine.Replace(error, string.Empty));
        }

        [Fact]
        public async Task ListScenarios_Given_AFileThatIsNotAProject_Then_PrintsWhatZppPrints()
        {
            string plan = Path.Combine(m_TempDirectory, @"not-a-plan.zpp");
            await File.WriteAllTextAsync(plan, @"This is not a plan.");
            (int exitCode, string output, string error) = await RunZppAsync(@"-i", plan, @"--list-scenarios");

            using var content = RunningServer.CompileContent(File.ReadAllBytes(plan), Path.GetFileName(plan));
            using HttpResponseMessage response = await m_Server!.Client.PostAsync(@"/v1/projects/scenarios?include=console", content);
            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            ConsoleResponse console = (await RunningServer.ReadProblemAsync(response)).Console.ShouldNotBeNull();

            console.ExitCode.ShouldBe(exitCode);
            console.StandardOutput.ShouldBe(output);
            console.StandardError.ShouldBe(WithoutLogLines(error));
        }

        // Runs the plan through zpp, with the options as flags and each output written under the name zpp serve gives
        // it, and through the server, and checks that the two end the same way - the exit code, the text, the files, and a
        // status that says whether the job succeeded.
        private async Task ShouldDoWhatZppDoesAsync(
            string planPath,
            CompileOptions options,
            HttpStatusCode expectedStatus = HttpStatusCode.OK,
            bool isImport = false)
        {
            string directory = Directory.CreateDirectory(Path.Combine(m_TempDirectory, @"zpp")).FullName;
            string filename = Path.GetFileName(planPath);
            string projectTitle = FileFormatHelper.GetProjectTitle(filename);

            (int exitCode, string output, string error) = await RunZppAsync(ToZppArguments(options, planPath, isImport, directory, projectTitle));

            using var content = RunningServer.CompileContent(File.ReadAllBytes(planPath), filename, options, isImport ? @"import" : @"project");
            using HttpResponseMessage message = await m_Server!.Client.PostAsync(@"/v1/projects/compile?include=console", content);
            message.StatusCode.ShouldBe(expectedStatus);

            // A job that exited with 0 is answered; any other is a problem, which has the console as well.
            (ConsoleResponse console, IReadOnlyList<OutputResponse> outputs) = expectedStatus == HttpStatusCode.OK
                ? await ReadAnswerAsync(message)
                : await ReadProblemAsync(message);

            console.ExitCode.ShouldBe(exitCode);
            (exitCode == 0).ShouldBe(expectedStatus == HttpStatusCode.OK);
            console.StandardOutput.ShouldBe(output);

            // zpp ends stderr's lines as the platform does; a response ends them all as it ends stdout's.
            console.StandardError.ShouldBe(WithoutLogLines(error));

            string[] written = [.. Directory.GetFiles(directory).Select(x => Path.GetFileName(x)).Order(StringComparer.Ordinal)];
            outputs.Select(x => x.FileName).Order(StringComparer.Ordinal).ShouldBe(written);

            foreach (OutputResponse produced in outputs)
            {
                produced.Content.ShouldBe(File.ReadAllBytes(Path.Combine(directory, produced.FileName)), produced.FileName);
                produced.ContentType.ShouldBe(CompileOptionsHelper.GetContentType(produced.FileName));
            }

            // Replayed through zpp's own console, the transcript prints exactly what zpp printed - stderr's lines ended
            // as zpp ends them on this platform - and it names each output once, in the order the job produced them.
            (string replayedOutput, string replayedError) = await ReplayAsync(console.Transcript);
            replayedOutput.ShouldBe(output);
            replayedError.ShouldBe(s_LogLine.Replace(error, string.Empty));
            console.Transcript.Where(x => x.Kind == JobTranscriptKind.Output).Select(x => x.Index)
                .ShouldBe(Enumerable.Range(0, outputs.Count).Select(x => (int?)x));
        }

        private static async Task<(ConsoleResponse Console, IReadOnlyList<OutputResponse> Outputs)> ReadAnswerAsync(HttpResponseMessage message)
        {
            CompileResponse response = await RunningServer.ReadAsync<CompileResponse>(message);
            return (response.Console.ShouldNotBeNull(), response.Outputs);
        }

        private static async Task<(ConsoleResponse Console, IReadOnlyList<OutputResponse> Outputs)> ReadProblemAsync(HttpResponseMessage message)
        {
            ProblemResponse problem = await RunningServer.ReadProblemAsync(message);
            return (problem.Console.ShouldNotBeNull(), problem.Outputs ?? []);
        }

        // Prints what a transcript records on zpp's own console, as zpp would have printed it: stdout as it came, and
        // stderr with its line ends the platform's, as zpp ends them there.
        private static async Task<(string Output, string Error)> ReplayAsync(IReadOnlyList<JobTranscriptEntry> transcript)
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            var console = new StandardConsole(output, error);

            foreach (JobTranscriptEntry entry in transcript)
            {
                switch (entry.Kind)
                {
                    case JobTranscriptKind.Line:
                        await console.WriteLineAsync(entry.Text.ShouldNotBeNull());
                        break;
                    case JobTranscriptKind.Display:
                        await console.DisplayAsync(entry.Text.ShouldNotBeNull(), entry.HasErrors);
                        break;
                    case JobTranscriptKind.ErrorLine:
                        await console.WriteErrorLineAsync(entry.Text.ShouldNotBeNull().ReplaceLineEndings(Environment.NewLine));
                        break;
                }
            }

            return (output.ToString(), error.ToString());
        }

        // zpp's arguments for what the options ask zpp serve for, writing each output to directory, under the name zpp
        // serve gives it.
        private static string[] ToZppArguments(
            CompileOptions options,
            string planPath,
            bool isImport,
            string directory,
            string projectTitle)
        {
            OutputsOptions outputs = options.Outputs ?? new OutputsOptions();

            List<string> args =
            [
                isImport ? @"--import" : @"--input", planPath,
                @"--base-theme", options.BaseTheme.ToString(),
                @"--metrics-format", options.MetricsFormat.ToString(),
            ];

            if (options.Scenario is not null)
            {
                args.AddRange([@"--scenario", options.Scenario]);
            }
            if (outputs.Project is not null)
            {
                args.AddRange([@"--output", Path.Combine(directory, CompileOptionsHelper.BuildOutputFilename(JobOutput.Project, options, projectTitle))]);
            }
            if (outputs.ScenarioExport is not null)
            {
                args.AddRange([@"--export", Path.Combine(directory, CompileOptionsHelper.BuildOutputFilename(JobOutput.ScenarioExport, options, projectTitle))]);
            }
            if (options.CompileTimeout is TimeSpan compileTimeout)
            {
                args.AddRange([@"--compile-timeout", ((int)compileTimeout.TotalMilliseconds).ToString(CultureInfo.InvariantCulture)]);
            }
            if (options.Now is not null)
            {
                args.AddRange([@"--now", options.Now]);
            }

            AddChart(args, @"gantt", outputs.GanttChart, directory);
            AddGraph(args, @"arrow", outputs.ArrowGraph, directory);
            AddGraph(args, @"vertex", outputs.VertexGraph, directory);
            AddChart(args, @"resource", outputs.ResourceChart, directory);
            AddChart(args, @"ev", outputs.EarnedValueChart, directory);
            AddChart(args, @"scenario-chart", outputs.ScenarioChart, directory);

            return [.. args];
        }

        private static void AddChart(
            List<string> args,
            string name,
            ChartOptions? chart,
            string directory)
        {
            if (chart is not null)
            {
                args.AddRange(
                [
                    $@"--{name}-directory", directory,
                    $@"--{name}-format", chart.Format.ToString(),
                    $@"--{name}-size", string.Create(CultureInfo.InvariantCulture, $@"{chart.Width}:{chart.Height}"),
                ]);
            }
        }

        private static void AddGraph(
            List<string> args,
            string name,
            GraphOptions? graph,
            string directory)
        {
            if (graph is not null)
            {
                args.AddRange([$@"--{name}-directory", directory, $@"--{name}-format", graph.Format.ToString()]);
            }
        }

        private static string WithoutLogLines(string error)
        {
            return NewLineHelper.NormalizeNewLines(s_LogLine.Replace(error, string.Empty));
        }

        // Runs zpp's Main with stdout and stderr captured.
        private static async Task<(int ExitCode, string Output, string Error)> RunZppAsync(params string[] args)
        {
            TextWriter originalOutput = Console.Out;
            TextWriter originalError = Console.Error;

            try
            {
                using var output = new StringWriter();
                using var error = new StringWriter();
                Console.SetOut(output);
                Console.SetError(error);

                int exitCode = await ZppMain.RunAsync(args);
                return (exitCode, output.ToString(), error.ToString());
            }
            finally
            {
                Console.SetOut(originalOutput);
                Console.SetError(originalError);
            }
        }
    }
}
