using Shouldly;
using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// zpp serve's promise: a job sent to it ends as zpp would have ended it,
    /// given the same options as flags - the same exit code, the same text on
    /// stdout and stderr, and the same files, byte for byte, under the same
    /// names - and its transcript, replayed through zpp's own console, prints
    /// exactly what zpp printed. Each test runs zpp's Main and the server on
    /// the same plan and compares the two. In the same collection as
    /// ProgramExitCodeTests, because Main swaps the console's streams while it
    /// runs.
    /// </summary>
    [Collection(ProgramExitCodeTests.CollectionName)]
    public class JobEndpointsParityTests
        : IClassFixture<EngineFixture>, IAsyncLifetime
    {
        private const string c_Now = @"2026-10-02T09:00:00+01:00";

        // A line zpp's log wrote on stderr. The server writes its jobs' log to its own log instead.
        private static readonly Regex s_LogLine = new(@"^\[\d\d:\d\d:\d\d [A-Z]{3}\] .*(\r?\n|$)", RegexOptions.Multiline);

        private readonly EngineFixture m_Engine;
        private readonly string m_TempDirectory;
        private RunningServer? m_Server;

        public JobEndpointsParityTests(EngineFixture engine)
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
        public async Task RunJob_Given_EveryOutputInRasterFormats_Then_DoesWhatZppDoes()
        {
            await ShouldDoWhatZppDoesAsync(SamplePath(), new JobOptions
            {
                Output = true,
                Export = true,
                MetricsFormat = MetricsExport.Json,
                Now = c_Now,
                Gantt = new ChartOptions { Format = PlotExport.Png, Width = 800, Height = 600 },
                Arrow = new GraphOptions { Format = GraphExport.Pdf },
                Vertex = new GraphOptions { Format = GraphExport.Png },
                Resource = new ChartOptions { Format = PlotExport.Jpeg, Width = 640, Height = 480 },
                EV = new ChartOptions { Format = PlotExport.Bmp, Width = 320, Height = 240 },
                ScenarioChart = new ChartOptions { Format = PlotExport.Webp, Width = 500, Height = 400 },
            });
        }

        [Fact]
        public async Task RunJob_Given_EveryOutputInVectorAndDataFormats_Then_DoesWhatZppDoes()
        {
            await ShouldDoWhatZppDoesAsync(SamplePath(), new JobOptions
            {
                BaseTheme = BaseTheme.Dark,
                MetricsFormat = MetricsExport.Markdown,
                Now = c_Now,
                Gantt = new ChartOptions { Format = PlotExport.Svg, Width = 800, Height = 600 },
                Arrow = new GraphOptions { Format = GraphExport.GraphML },
                Vertex = new GraphOptions { Format = GraphExport.Dot },
                Resource = new ChartOptions { Format = PlotExport.Svg, Width = 800, Height = 600 },
                EV = new ChartOptions { Format = PlotExport.Svg, Width = 800, Height = 600 },
                ScenarioChart = new ChartOptions { Format = PlotExport.Svg, Width = 800, Height = 600 },
            });
        }

        [Fact]
        public async Task RunJob_Given_TableMetricsAndJpegGraphs_Then_DoesWhatZppDoes()
        {
            await ShouldDoWhatZppDoesAsync(SamplePath(), new JobOptions
            {
                MetricsFormat = MetricsExport.Table,
                CompileTimeout = 30_000,
                Arrow = new GraphOptions { Format = GraphExport.Jpeg },
                Vertex = new GraphOptions { Format = GraphExport.Svg },
            });
        }

        [Fact]
        public async Task RunJob_Given_AScenarioByName_Then_DoesWhatZppDoes()
        {
            await ShouldDoWhatZppDoesAsync(AssetPath(@"two-scenarios.zpp"), new JobOptions
            {
                Scenario = @"Beta",
                Output = true,
                Now = c_Now,
                Gantt = new ChartOptions { Format = PlotExport.Svg, Width = 800, Height = 600 },
            });
        }

        [Fact]
        public async Task RunJob_Given_AScenarioThatIsNotThere_Then_DoesWhatZppDoes()
        {
            await ShouldDoWhatZppDoesAsync(AssetPath(@"two-scenarios.zpp"), new JobOptions
            {
                Scenario = @"Gamma",
            });
        }

        [Fact]
        public async Task RunJob_Given_APlanThatDoesNotCompile_Then_DoesWhatZppDoes()
        {
            await ShouldDoWhatZppDoesAsync(AssetPath(@"broken-dependency.zpp"), new JobOptions
            {
                Output = true,
                Gantt = new ChartOptions { Format = PlotExport.Png, Width = 800, Height = 600 },
            });
        }

        [Fact]
        public async Task RunJob_Given_AWorkbookToImport_Then_DoesWhatZppDoes()
        {
            // The workbook zpp exports from the sample, at a fixed time, so that both import the same bytes.
            string workbook = Path.Combine(m_TempDirectory, @"plan.xlsx");
            (await RunZppAsync(@"-i", SamplePath(), @"-x", workbook, @"--now", c_Now)).ExitCode.ShouldBe(0);

            // Not the project: an import gives the plan's scenario a new id each time.
            await ShouldDoWhatZppDoesAsync(workbook, new JobOptions
            {
                Export = true,
                MetricsFormat = MetricsExport.Json,
                Now = c_Now,
                Gantt = new ChartOptions { Format = PlotExport.Png, Width = 800, Height = 600 },
                Vertex = new GraphOptions { Format = GraphExport.Svg },
            }, isImport: true);
        }

        [Fact]
        public async Task ListScenarios_Given_AProject_Then_PrintsWhatZppPrints()
        {
            string asset = AssetPath(@"two-scenarios.zpp");
            (int exitCode, string output, string error) = await RunZppAsync(@"-i", asset, @"--list-scenarios");

            using var content = RunningServer.JobContent(File.ReadAllBytes(asset), Path.GetFileName(asset));
            using HttpResponseMessage response = await m_Server!.Client.PostAsync(@"/v1/scenarios", content);
            response.EnsureSuccessStatusCode();
            ScenariosResponse scenarios = await RunningServer.ReadAsync<ScenariosResponse>(response);

            scenarios.ExitCode.ShouldBe(exitCode);
            scenarios.Stdout.ShouldBe(output);
            scenarios.Stderr.ShouldBe(WithoutLogLines(error));
            scenarios.Scenarios.ShouldNotBeNull().Select(x => x.Path).ShouldBe([@"Alpha", @"Beta"]);

            (string replayedOutput, string replayedError) = await ReplayAsync(scenarios.Transcript);
            replayedOutput.ShouldBe(output);
            replayedError.ShouldBe(s_LogLine.Replace(error, string.Empty));
        }

        // Runs the plan through zpp, with the options as flags and each output written under the name zpp serve gives
        // it, and through the server, and checks that the two end the same way.
        private async Task ShouldDoWhatZppDoesAsync(
            string planPath,
            JobOptions options,
            bool isImport = false)
        {
            string directory = Directory.CreateDirectory(Path.Combine(m_TempDirectory, @"zpp")).FullName;
            string filename = Path.GetFileName(planPath);
            string projectTitle = SettingServiceBase.GetProjectTitle(filename);

            (int exitCode, string output, string error) = await RunZppAsync(ToZppArguments(options, planPath, isImport, directory, projectTitle));

            using var content = RunningServer.JobContent(File.ReadAllBytes(planPath), filename, options, isImport ? @"import" : @"input");
            using HttpResponseMessage message = await m_Server!.Client.PostAsync(@"/v1/jobs", content);
            message.EnsureSuccessStatusCode();
            JobResponse response = await RunningServer.ReadAsync<JobResponse>(message);

            response.ExitCode.ShouldBe(exitCode);
            response.Stdout.ShouldBe(output);
            // zpp ends stderr's lines as the platform does; a response ends them all as it ends stdout's.
            response.Stderr.ShouldBe(WithoutLogLines(error));

            string[] written = [.. Directory.GetFiles(directory).Select(x => Path.GetFileName(x)).Order(StringComparer.Ordinal)];
            response.Outputs.Select(x => x.FileName).Order(StringComparer.Ordinal).ShouldBe(written);

            foreach (JobResponseOutput produced in response.Outputs)
            {
                produced.Content.ShouldBe(File.ReadAllBytes(Path.Combine(directory, produced.FileName)), produced.FileName);
                produced.ContentType.ShouldBe(JobOptionsHelper.GetContentType(produced.FileName));
            }

            // Replayed through zpp's own console, the transcript prints exactly what zpp printed - stderr's lines ended
            // as zpp ends them on this platform - and it names each output once, in the order the job produced them.
            (string replayedOutput, string replayedError) = await ReplayAsync(response.Transcript);
            replayedOutput.ShouldBe(output);
            replayedError.ShouldBe(s_LogLine.Replace(error, string.Empty));
            response.Transcript.Where(x => x.Kind == JobTranscriptKind.Output).Select(x => x.Index)
                .ShouldBe(Enumerable.Range(0, response.Outputs.Count).Select(x => (int?)x));
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
            JobOptions options,
            string planPath,
            bool isImport,
            string directory,
            string projectTitle)
        {
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
            if (options.Output)
            {
                args.AddRange([@"--output", Path.Combine(directory, JobOptionsHelper.BuildOutputFilename(JobOutput.Project, options, projectTitle))]);
            }
            if (options.Export)
            {
                args.AddRange([@"--export", Path.Combine(directory, JobOptionsHelper.BuildOutputFilename(JobOutput.ScenarioExport, options, projectTitle))]);
            }
            if (options.CompileTimeout is int compileTimeout)
            {
                args.AddRange([@"--compile-timeout", compileTimeout.ToString(CultureInfo.InvariantCulture)]);
            }
            if (options.Now is not null)
            {
                args.AddRange([@"--now", options.Now]);
            }

            AddChart(args, @"gantt", options.Gantt, directory);
            AddGraph(args, @"arrow", options.Arrow, directory);
            AddGraph(args, @"vertex", options.Vertex, directory);
            AddChart(args, @"resource", options.Resource, directory);
            AddChart(args, @"ev", options.EV, directory);
            AddChart(args, @"scenario-chart", options.ScenarioChart, directory);

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

                int exitCode = await Program.Main(args);
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
