using Newtonsoft.Json.Linq;
using Shouldly;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// End-to-end tests that invoke Program.Main in-process and pin the CLI's
    /// exit-code contract: 0 success, 1 runtime failure, 2 bad usage, 3
    /// compilation errors, and 4 a compilation cancelled by --compile-timeout.
    /// Scripts and CI gates branch on these values, so a change here is a
    /// breaking change to the CLI. The tests all live in one
    /// class so xunit runs them sequentially - Main swaps process-global state
    /// (the console streams and the static Serilog logger) while it runs.
    /// </summary>
    public class ProgramExitCodeTests
        : IDisposable
    {
        private readonly string m_TempDirectory;

        public ProgramExitCodeTests()
        {
            m_TempDirectory = Path.Combine(Path.GetTempPath(), $@"zpp-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(m_TempDirectory);
        }

        private static string AssetPath(string filename)
        {
            return Path.Combine(AppContext.BaseDirectory, @"Assets", filename);
        }

        private static async Task<(int ExitCode, string Output)> RunCapturedAsync(params string[] args)
        {
            TextWriter original = Console.Out;
            try
            {
                using var writer = new StringWriter();
                Console.SetOut(writer);
                int exitCode = await Program.Main(args);
                return (exitCode, writer.ToString());
            }
            finally
            {
                Console.SetOut(original);
            }
        }

        [Fact]
        public async Task Main_Given_ValidProject_Then_ExitSuccess()
        {
            int exitCode = await Program.Main([@"-i", AssetPath(@"two-scenarios.zpp")]);

            exitCode.ShouldBe(0);
        }

        [Fact]
        public async Task Main_Given_ListScenarios_Then_ListsBothAndExitSuccess()
        {
            (int exitCode, string output) = await RunCapturedAsync(@"-i", AssetPath(@"two-scenarios.zpp"), @"--list-scenarios");

            exitCode.ShouldBe(0);
            output.ShouldContain(@"Alpha");
            output.ShouldContain(@"Beta");
        }

        [Fact]
        public async Task Main_Given_ScenarioSwitchAndSave_Then_CurrentPersisted()
        {
            // The scenario id is read out of the asset rather than hard-coded so
            // the test survives the asset being regenerated.
            JObject asset = JObject.Parse(File.ReadAllText(AssetPath(@"two-scenarios.zpp")));
            string betaId = asset[@"Nodes"]!
                .First(x => string.Equals((string?)x[@"Name"], @"Beta", StringComparison.Ordinal))[@"Id"]!
                .ToString();

            string outputFile = Path.Combine(m_TempDirectory, @"switched.zpp");

            int exitCode = await Program.Main([@"-i", AssetPath(@"two-scenarios.zpp"), @"-s", @"Beta", @"-o", outputFile]);

            exitCode.ShouldBe(0);
            JObject saved = JObject.Parse(File.ReadAllText(outputFile));
            saved[@"Current"]!.ToString().ShouldBe(betaId);
        }

        [Fact]
        public async Task Main_Given_ScenarioIdPrefix_Then_CurrentPersisted()
        {
            JObject asset = JObject.Parse(File.ReadAllText(AssetPath(@"two-scenarios.zpp")));
            string betaId = asset[@"Nodes"]!
                .First(x => string.Equals((string?)x[@"Name"], @"Beta", StringComparison.Ordinal))[@"Id"]!
                .ToString();
            string betaIdPrefix = betaId[..8];

            string outputFile = Path.Combine(m_TempDirectory, @"switched-by-prefix.zpp");

            int exitCode = await Program.Main([@"-i", AssetPath(@"two-scenarios.zpp"), @"-s", betaIdPrefix, @"-o", outputFile]);

            exitCode.ShouldBe(0);
            JObject saved = JObject.Parse(File.ReadAllText(outputFile));
            saved[@"Current"]!.ToString().ShouldBe(betaId);
        }

        [Fact]
        public async Task Main_Given_UnknownScenario_Then_ExitFailure()
        {
            int exitCode = await Program.Main([@"-i", AssetPath(@"two-scenarios.zpp"), @"-s", @"No Such Scenario"]);

            exitCode.ShouldBe(1);
        }

        [Fact]
        public async Task Main_Given_InputAndImport_Then_ExitUsageError()
        {
            int exitCode = await Program.Main([@"-i", AssetPath(@"two-scenarios.zpp"), @"-m", AssetPath(@"two-scenarios.zpp")]);

            exitCode.ShouldBe(2);
        }

        [Fact]
        public async Task Main_Given_ScenarioWithImport_Then_ExitUsageError()
        {
            int exitCode = await Program.Main([@"-m", AssetPath(@"two-scenarios.zpp"), @"-s", @"Beta"]);

            exitCode.ShouldBe(2);
        }

        [Fact]
        public async Task Main_Given_NegativeCompileTimeout_Then_ExitUsageError()
        {
            int exitCode = await Program.Main([@"-i", AssetPath(@"two-scenarios.zpp"), @"--compile-timeout", @"-1"]);

            exitCode.ShouldBe(2);
        }

        [Fact]
        public async Task Main_Given_CompileTimeoutZero_Then_ExitSuccess()
        {
            // Zero switches the watchdog off for the whole run, so the compile and
            // every output build have to complete without one. Exit code 4 - the
            // watchdog firing - has no test here: the smallest budget the timer can
            // reliably signal is coarser than the time this asset takes to compile,
            // so any attempt to provoke it would be a race.
            int exitCode = await Program.Main([@"-i", AssetPath(@"two-scenarios.zpp"), @"--compile-timeout", @"0"]);

            exitCode.ShouldBe(0);
        }

        [Fact]
        public async Task Main_Given_DirectoryWithoutSize_Then_ExitUsageError()
        {
            int exitCode = await Program.Main([@"-i", AssetPath(@"two-scenarios.zpp"), @"--gantt-directory", m_TempDirectory]);

            exitCode.ShouldBe(2);
        }

        [Fact]
        public async Task Main_Given_MissingExportDirectory_Then_ExitFailure()
        {
            string missingDirectory = Path.Combine(m_TempDirectory, @"does-not-exist");

            int exitCode = await Program.Main([@"-i", AssetPath(@"two-scenarios.zpp"), @"--gantt-directory", missingDirectory, @"--gantt-size", @"800:600"]);

            exitCode.ShouldBe(1);
        }

        [Fact]
        public async Task Main_Given_BrokenDependencies_Then_ExitCompilationErrors()
        {
            int exitCode = await Program.Main([@"-i", AssetPath(@"broken-dependency.zpp")]);

            exitCode.ShouldBe(3);
        }

        [Fact]
        public async Task Main_Given_Help_Then_ExitSuccess()
        {
            (int exitCode, _) = await RunCapturedAsync(@"--help");

            exitCode.ShouldBe(0);
        }

        [Fact]
        public async Task Main_Given_UnknownOption_Then_ExitUsageError()
        {
            (int exitCode, _) = await RunCapturedAsync(@"--nonsense");

            exitCode.ShouldBe(2);
        }

        [Fact]
        public async Task Main_Given_NoArguments_Then_ExitUsageError()
        {
            (int exitCode, _) = await RunCapturedAsync();

            exitCode.ShouldBe(2);
        }

        [Fact]
        public async Task Main_Given_JsonMetricsFormat_Then_EmitsParseableJson()
        {
            (int exitCode, string output) = await RunCapturedAsync(@"-i", AssetPath(@"two-scenarios.zpp"), @"--metrics-format", @"json");

            exitCode.ShouldBe(0);

            // JSON mode must emit nothing but the JSON document on stdout, so
            // the whole capture has to parse.
            JObject metrics = JObject.Parse(output);
            metrics[@"TotalCost"].ShouldNotBeNull();
            metrics[@"ProjectFinish"].ShouldNotBeNull();
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(m_TempDirectory, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup only.
            }

            GC.SuppressFinalize(this);
        }
    }
}
