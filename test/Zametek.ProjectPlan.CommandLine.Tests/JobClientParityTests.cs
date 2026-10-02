using Shouldly;
using System.Text.RegularExpressions;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// zpp --server's promise: a run sent to a server ends as it would have
    /// ended in this process - the same exit code, the same text on stdout and
    /// stderr, and the same files, byte for byte, under the same names - even
    /// when one of its files cannot be written here. Each test runs one command
    /// line through zpp's Main twice, in this process and on a server, each run
    /// writing to a directory of its own, and compares the two. Only zpp's log
    /// lines, which a server keeps in its own log, may differ. In the same
    /// collection as ProgramExitCodeTests, because Main swaps the console's
    /// streams while it runs.
    /// </summary>
    [Collection(ProgramExitCodeTests.CollectionName)]
    public class JobClientParityTests
        : IClassFixture<EngineFixture>, IAsyncLifetime
    {
        private const string c_Now = @"2026-10-02T09:00:00+01:00";

        // A line zpp's log wrote on stderr.
        private static readonly Regex s_LogLine = new(@"^\[\d\d:\d\d:\d\d [A-Z]{3}\] .*(\r?\n|$)", RegexOptions.Multiline);

        private readonly EngineFixture m_Engine;
        private readonly string m_TempDirectory;
        private RunningServer? m_Server;

        public JobClientParityTests(EngineFixture engine)
        {
            m_Engine = engine;
            m_TempDirectory = Path.Combine(Path.GetTempPath(), $@"zpp-client-parity-{Guid.NewGuid():N}");
            Directory.CreateDirectory(m_TempDirectory);
        }

        private RunningServer Server => m_Server ?? throw new InvalidOperationException();

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
        public async Task Run_Given_EveryOutputInRasterFormats_Then_DoesWhatZppDoesHere()
        {
            await ShouldDoWhatZppDoesHereAsync(directory =>
            [
                @"-i", SamplePath(),
                @"-o", Path.Combine(directory, @"saved.zpp"),
                @"-x", Path.Combine(directory, @"exported.xlsx"),
                @"--metrics-format", @"json",
                @"--now", c_Now,
                @"--gantt-directory", directory, @"--gantt-format", @"png", @"--gantt-size", @"800:600",
                @"--arrow-directory", directory, @"--arrow-format", @"pdf",
                @"--vertex-directory", directory, @"--vertex-format", @"png",
                @"--resource-directory", directory, @"--resource-format", @"jpeg", @"--resource-size", @"640:480",
                @"--ev-directory", directory, @"--ev-format", @"bmp", @"--ev-size", @"320:240",
                @"--scenario-chart-directory", directory, @"--scenario-chart-format", @"webp", @"--scenario-chart-size", @"500:400",
            ]);
        }

        [Fact]
        public async Task Run_Given_EveryOutputInVectorAndDataFormats_Then_DoesWhatZppDoesHere()
        {
            await ShouldDoWhatZppDoesHereAsync(directory =>
            [
                @"-i", SamplePath(),
                @"--base-theme", @"Dark",
                @"--now", c_Now,
                @"--gantt-directory", directory, @"--gantt-format", @"svg", @"--gantt-size", @"800:600",
                @"--arrow-directory", directory, @"--arrow-format", @"graphml",
                @"--vertex-directory", directory, @"--vertex-format", @"dot",
                @"--resource-directory", directory, @"--resource-format", @"svg", @"--resource-size", @"800:600",
                @"--ev-directory", directory, @"--ev-format", @"svg", @"--ev-size", @"800:600",
                @"--scenario-chart-directory", directory, @"--scenario-chart-format", @"svg", @"--scenario-chart-size", @"800:600",
            ]);
        }

        [Fact]
        public async Task Run_Given_TableMetricsAndJpegGraphs_Then_DoesWhatZppDoesHere()
        {
            await ShouldDoWhatZppDoesHereAsync(directory =>
            [
                @"-i", SamplePath(),
                @"--metrics-format", @"table",
                @"--compile-timeout", @"30000",
                @"--arrow-directory", directory, @"--arrow-format", @"jpeg",
                @"--vertex-directory", directory, @"--vertex-format", @"svg",
            ]);
        }

        [Fact]
        public async Task Run_Given_AScenarioByNameFromAPlanWhoseNameIsNotAscii_Then_DoesWhatZppDoesHere()
        {
            // The outputs are named after the plan, which goes to the server under its own name.
            string plan = Path.Combine(m_TempDirectory, @"Plan é ü.zpp");
            File.Copy(AssetPath(@"two-scenarios.zpp"), plan, overwrite: true);

            await ShouldDoWhatZppDoesHereAsync(directory =>
            [
                @"-i", plan,
                @"-s", @"Beta",
                @"-o", Path.Combine(directory, @"beta.zpp"),
                @"--now", c_Now,
                @"--gantt-directory", directory, @"--gantt-format", @"svg", @"--gantt-size", @"800:600",
            ]);
        }

        [Fact]
        public async Task Run_Given_APlanWhoseNameGivesItNoTitle_Then_DoesWhatZppDoesHere()
        {
            // zpp names the chart after a plan with no title all the same; the server, which needs one to name its
            // outputs, is sent the plan under a stand-in.
            string plan = Path.Combine(m_TempDirectory, @".zpp");
            File.Copy(AssetPath(@"two-scenarios.zpp"), plan, overwrite: true);

            await ShouldDoWhatZppDoesHereAsync(directory =>
            [
                @"-i", plan,
                @"--now", c_Now,
                @"--gantt-directory", directory, @"--gantt-format", @"svg", @"--gantt-size", @"800:600",
            ]);
        }

        [Fact]
        public async Task Run_Given_AScenarioThatIsNotThere_Then_DoesWhatZppDoesHere()
        {
            await ShouldDoWhatZppDoesHereAsync(directory => [@"-i", AssetPath(@"two-scenarios.zpp"), @"-s", @"Gamma"]);
        }

        [Fact]
        public async Task Run_Given_APlanThatDoesNotCompile_Then_DoesWhatZppDoesHere()
        {
            await ShouldDoWhatZppDoesHereAsync(directory =>
            [
                @"-i", AssetPath(@"broken-dependency.zpp"),
                @"-o", Path.Combine(directory, @"broken.zpp"),
                @"--gantt-directory", directory, @"--gantt-format", @"png", @"--gantt-size", @"800:600",
            ]);
        }

        [Fact]
        public async Task Run_Given_AWorkbookToImport_Then_DoesWhatZppDoesHere()
        {
            // The workbook zpp exports from the sample, at a fixed time, so that both runs import the same bytes.
            string workbook = Path.Combine(m_TempDirectory, @"plan.xlsx");
            (await ZppMain.RunAsync([@"-i", SamplePath(), @"-x", workbook, @"--now", c_Now])).ShouldBe(0);

            // Not the project: an import gives the plan's scenario a new id each time.
            await ShouldDoWhatZppDoesHereAsync(directory =>
            [
                @"-m", workbook,
                @"-x", Path.Combine(directory, @"exported.xlsx"),
                @"--metrics-format", @"json",
                @"--now", c_Now,
                @"--gantt-directory", directory, @"--gantt-format", @"png", @"--gantt-size", @"800:600",
                @"--vertex-directory", directory, @"--vertex-format", @"svg",
            ]);
        }

        [Fact]
        public async Task ListScenarios_Given_AProject_Then_DoesWhatZppDoesHere()
        {
            await ShouldDoWhatZppDoesHereAsync(directory => [@"-i", AssetPath(@"two-scenarios.zpp"), @"--list-scenarios"]);
        }

        [Fact]
        public async Task Run_Given_AChartThatCannotBeWrittenHere_Then_ReportsItAndCarriesOnAsZppDoes()
        {
            // A directory where the Gantt chart's file would go: zpp says why it cannot write the chart, writes the
            // graph after it, prints the metrics, and fails.
            await ShouldDoWhatZppDoesHereAsync(
                directory =>
                [
                    @"-i", SamplePath(),
                    @"--gantt-directory", directory, @"--gantt-format", @"png", @"--gantt-size", @"800:600",
                    @"--arrow-directory", directory, @"--arrow-format", @"svg",
                ],
                directory => Directory.CreateDirectory(Path.Combine(directory, @"sample-gantt.png")),
                expectedExitCode: ExitCode.Failure);
        }

        [Fact]
        public async Task Run_Given_AProjectThatCannotBeWrittenHere_Then_StopsAsZppDoes()
        {
            // A directory where the project's file would go: zpp says why, and stops - before the chart, and before
            // the metrics.
            await ShouldDoWhatZppDoesHereAsync(
                directory =>
                [
                    @"-i", SamplePath(),
                    @"-o", Path.Combine(directory, @"saved.zpp"),
                    @"--gantt-directory", directory, @"--gantt-format", @"png", @"--gantt-size", @"800:600",
                ],
                directory => Directory.CreateDirectory(Path.Combine(directory, @"saved.zpp")),
                expectedExitCode: ExitCode.Failure);
        }

        [Fact]
        public async Task Run_Given_AServerOnAUnixDomainSocket_Then_DoesWhatZppDoesHere()
        {
            // A path short enough for every platform's limit on a socket's.
            string socket = Path.Combine(Path.GetTempPath(), $@"zpp-{Guid.NewGuid():N}"[..12] + @".sock");
            await using RunningServer server = await RunningServer.StartAsync(m_Engine.JobRunner, new ServeSettings { UnixSocket = socket });

            await ShouldDoWhatZppDoesHereAsync(
                directory =>
                [
                    @"-i", SamplePath(),
                    @"-o", Path.Combine(directory, @"saved.zpp"),
                    @"--now", c_Now,
                    @"--gantt-directory", directory, @"--gantt-format", @"png", @"--gantt-size", @"800:600",
                ],
                server: server,
                address: $@"unix:{socket}");
        }

        [Fact]
        public async Task Run_Given_TheServerInTheEnvironment_Then_DoesWhatZppDoesHere()
        {
            await ShouldDoWhatZppDoesHereAsync(
                directory =>
                [
                    @"-i", SamplePath(),
                    @"-o", Path.Combine(directory, @"saved.zpp"),
                    @"--now", c_Now,
                    @"--vertex-directory", directory, @"--vertex-format", @"svg",
                ],
                viaEnvironment: true);
        }

        // Runs the command line - with the paths of its outputs in each run's own directory - in this process and on the
        // server, and checks that the two end the same way: the same exit code, the same text, and the same files - and
        // that the second run did go to the server.
        private async Task ShouldDoWhatZppDoesHereAsync(
            Func<string, string[]> commandLine,
            Action<string>? prepare = null,
            ExitCode? expectedExitCode = null,
            RunningServer? server = null,
            string? address = null,
            bool viaEnvironment = false)
        {
            string here = Directory.CreateDirectory(Path.Combine(m_TempDirectory, $@"here-{Guid.NewGuid():N}")).FullName;
            string there = Directory.CreateDirectory(Path.Combine(m_TempDirectory, $@"server-{Guid.NewGuid():N}")).FullName;
            prepare?.Invoke(here);
            prepare?.Invoke(there);

            (int exitCode, string output, string error) = await ZppMain.RunCapturedAsync(commandLine(here));

            server ??= Server;
            address ??= server.Address;
            int requests = server.ApiRequests;

            (int serverExitCode, string serverOutput, string serverError) = viaEnvironment
                ? await ZppMain.RunCapturedAsync(commandLine(there), new Dictionary<string, string> { [ClientSettingsHelper.ServerVariable] = address })
                : await ZppMain.RunCapturedAsync([.. commandLine(there), @"--server", address]);

            server.ApiRequests.ShouldBe(requests + 1);

            if (expectedExitCode is ExitCode expected)
            {
                exitCode.ShouldBe((int)expected);
            }

            serverExitCode.ShouldBe(exitCode);
            serverOutput.ShouldBe(output);

            // Any path in a message is the run's own.
            WithoutLogLines(serverError).Replace(there, @"<directory>", StringComparison.Ordinal)
                .ShouldBe(WithoutLogLines(error).Replace(here, @"<directory>", StringComparison.Ordinal));

            string[] written = Files(here);
            Files(there).ShouldBe(written);

            foreach (string file in written)
            {
                File.ReadAllBytes(Path.Combine(there, file)).ShouldBe(File.ReadAllBytes(Path.Combine(here, file)), file);
            }
        }

        private static string[] Files(string directory)
        {
            return [.. Directory.GetFiles(directory, @"*", SearchOption.AllDirectories)
                .Select(x => Path.GetRelativePath(directory, x))
                .Order(StringComparer.Ordinal)];
        }

        private static string WithoutLogLines(string error)
        {
            return s_LogLine.Replace(error, string.Empty);
        }
    }
}
