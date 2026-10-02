using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shouldly;
using System.IO.Compression;
using Xunit;
using Zametek.Utility;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// End-to-end tests that invoke Program.Main in-process and pin the CLI's
    /// exit-code contract: 0 success, 1 runtime failure, 2 bad usage, 3
    /// compilation errors, and 4 a compilation cancelled by --compile-timeout.
    /// Scripts and CI gates branch on these values, so a change here is a
    /// breaking change to the CLI. Checks on the files a run writes and on what
    /// it prints are here as well, since they need Main too. The tests all live
    /// in one collection - with JobEndpointsParityTests, which run Main as well -
    /// so xunit runs them one at a time: Main swaps process-global state (the
    /// console streams and the static Serilog logger) while it runs.
    /// </summary>
    [Collection(CollectionName)]
    public class ProgramExitCodeTests
        : IDisposable
    {
        // The collection of the tests that run Main.
        public const string CollectionName = @"Program.Main";

        // How much longer than a fresh export the file already sitting at the
        // output path is made. It has to be more than 64 KiB, because that is as
        // far back from the end of a file as a zip reader searches for the
        // central directory: with a shorter stale tail the reader still finds the
        // new directory, and the XLSX test would pass with the stale bytes left
        // in place.
        private const int c_StaleFileExcessLength = 128 * 1024;

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

        // Runs Main with stderr captured as well as stdout.
        private static async Task<(int ExitCode, string Output, string Error)> RunCapturedWithErrorAsync(params string[] args)
        {
            TextWriter original = Console.Error;
            try
            {
                using var writer = new StringWriter();
                Console.SetError(writer);
                (int exitCode, string output) = await RunCapturedAsync(args);
                return (exitCode, output, writer.ToString());
            }
            finally
            {
                Console.SetError(original);
            }
        }

        // Runs one export twice: first into an empty directory, then into a
        // directory where a larger file already sits at the output path, as a
        // rerun into the same output directory finds it. Returns both output
        // files so the caller can check the second against the first.
        private async Task<(string FreshFile, string OverwrittenFile)> ExportFreshThenOverLargerFileAsync(
            Func<string, string> buildOutputFilename,
            Func<string, string[]> buildArgs)
        {
            string freshDirectory = Path.Combine(m_TempDirectory, @"fresh");
            string staleDirectory = Path.Combine(m_TempDirectory, @"stale");
            Directory.CreateDirectory(freshDirectory);
            Directory.CreateDirectory(staleDirectory);

            string freshFile = buildOutputFilename(freshDirectory);
            string overwrittenFile = buildOutputFilename(staleDirectory);

            int freshExitCode = await Program.Main(buildArgs(freshDirectory));
            freshExitCode.ShouldBe(0);

            File.WriteAllText(overwrittenFile, new string('X', (int)new FileInfo(freshFile).Length + c_StaleFileExcessLength));

            int overwriteExitCode = await Program.Main(buildArgs(staleDirectory));
            overwriteExitCode.ShouldBe(0);

            return (freshFile, overwrittenFile);
        }

        // Runs Main while the file an export is about to write is held open with
        // no sharing, so the export's own write fails the way it does when a
        // viewer has the file open: a sharing violation on Windows, and on Unix
        // a clash with the flock that .NET takes for FileShare.None.
        private static async Task<(int ExitCode, string Output)> RunCapturedWithFileLockedAsync(
            string lockedFile,
            params string[] args)
        {
            using var lockStream = new FileStream(lockedFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            return await RunCapturedAsync(args);
        }

        // The text has lines, and each of them ends with NewLineHelper.NewLine: there is no carriage return anywhere.
        private static void ShouldEndLinesWithNewLine(string text)
        {
            text.ShouldContain(NewLineHelper.NewLine);
            text.ShouldNotContain(NewLineHelper.CarriageReturn);
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
        public async Task Main_Given_ImportAndOutput_Then_SavedProjectHoldsTheImportedScenario()
        {
            // An import lands in the current scenario of the open project, which on
            // the desktop is the Base scenario every new project starts with. zpp
            // starts its project the same way, so the plan it imports is the plan
            // it saves, not an empty project.
            JObject asset = JObject.Parse(File.ReadAllText(AssetPath(@"two-scenarios.zpp")));
            string assetCurrentId = asset[@"Current"]!.ToString();
            List<(int, string)> expectedActivities = [.. asset[@"Files"]!
                .First(x => string.Equals((string?)x[@"NodeId"], assetCurrentId, StringComparison.Ordinal))[@"Scenario"]![@"DependentActivities"]!
                .Select(x => ((int)x[@"Activity"]![@"Id"]!, (string)x[@"Activity"]![@"Name"]!))];

            string workbook = Path.Combine(m_TempDirectory, @"plan.xlsx");
            string outputFile = Path.Combine(m_TempDirectory, @"imported.zpp");
            (await Program.Main([@"-i", AssetPath(@"two-scenarios.zpp"), @"-x", workbook])).ShouldBe(0);

            int exitCode = await Program.Main([@"-m", workbook, @"-o", outputFile]);

            exitCode.ShouldBe(0);
            JObject saved = JObject.Parse(File.ReadAllText(outputFile));
            JToken node = saved[@"Nodes"]!.ShouldHaveSingleItem();
            node[@"Name"]!.ToString().ShouldBe(Resource.ProjectPlan.Labels.Label_BaseNode);
            saved[@"Current"]!.ToString().ShouldBe(node[@"Id"]!.ToString());
            JToken file = saved[@"Files"]!.ShouldHaveSingleItem();
            file[@"NodeId"]!.ToString().ShouldBe(node[@"Id"]!.ToString());
            file[@"Scenario"]![@"DependentActivities"]!
                .Select(x => ((int)x[@"Activity"]![@"Id"]!, (string)x[@"Activity"]![@"Name"]!))
                .ShouldBe(expectedActivities);
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
        public async Task Main_Given_NowWithoutAnOffset_Then_ExitUsageError()
        {
            int exitCode = await Program.Main([@"-i", AssetPath(@"two-scenarios.zpp"), @"--now", @"2026-09-27T12:00:00"]);

            exitCode.ShouldBe(2);
        }

        [Fact]
        public async Task Main_Given_Now_Then_EveryRunSavesAndExportsTheSameFiles()
        {
            var now = new DateTimeOffset(2001, 2, 3, 4, 5, 6, TimeSpan.FromHours(-5));
            string[] Args(string directory) =>
            [
                @"-i", AssetPath(@"two-scenarios.zpp"),
                @"-o", Path.Combine(directory, @"plan.zpp"),
                @"-x", Path.Combine(directory, @"plan.xlsx"),
                @"--now", @"2001-02-03T04:05:06-05:00",
            ];
            string first = Directory.CreateDirectory(Path.Combine(m_TempDirectory, @"first")).FullName;
            string second = Directory.CreateDirectory(Path.Combine(m_TempDirectory, @"second")).FullName;

            (await Program.Main(Args(first))).ShouldBe(0);
            (await Program.Main(Args(second))).ShouldBe(0);

            // The run happened at the time it was given...
            using var reader = new JsonTextReader(new StringReader(File.ReadAllText(Path.Combine(first, @"plan.zpp"))))
            {
                DateParseHandling = DateParseHandling.DateTimeOffset,
            };
            JObject saved = JObject.Load(reader);
            JToken current = saved[@"Nodes"]!.Single(x => x[@"Id"]!.ToString() == saved[@"Current"]!.ToString());
            current[@"ModifiedOn"]!.Value<DateTimeOffset>().ShouldBe(now);

            // ...so the second run wrote exactly what the first did.
            File.ReadAllBytes(Path.Combine(second, @"plan.zpp")).ShouldBe(File.ReadAllBytes(Path.Combine(first, @"plan.zpp")));
            File.ReadAllBytes(Path.Combine(second, @"plan.xlsx")).ShouldBe(File.ReadAllBytes(Path.Combine(first, @"plan.xlsx")));
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
        public async Task Main_Given_ValidFile_Then_ChartsUseTheBundledFont()
        {
            (int exitCode, _) = await RunCapturedAsync(@"-i", AssetPath(@"two-scenarios.zpp"));

            exitCode.ShouldBe(0);

            // ScottPlot's default font is process-wide, and only Main sets it in this test run.
            ScottPlot.Fonts.Default.ShouldBe(ChartFonts.FamilyName);
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

        [Theory]
        [InlineData(GraphExport.Svg)]
        [InlineData(GraphExport.GraphML)]
        [InlineData(GraphExport.Dot)]
        [InlineData(GraphExport.Pdf)]
        [InlineData(GraphExport.Png)]
        [InlineData(GraphExport.Jpeg)]
        public async Task Main_Given_GraphExportOverLargerFile_Then_SameLengthAsFreshExport(GraphExport format)
        {
            // Rerunning into the same output directory is routine for a CI job,
            // so an export has to replace the file it finds rather than write over
            // the front of it and leave the old tail behind. Graph exports come
            // out byte-for-byte the same on every run, so any difference in length
            // is left-over bytes. SVG, GraphML and Dot are written as they are;
            // PDF, PNG and JPEG are rasterised first and saved by a separate path.
            (string freshFile, string overwrittenFile) = await ExportFreshThenOverLargerFileAsync(
                directory => Program.BuildExportFilePath(directory, @"two-scenarios", Resource.ProjectPlan.Suffixes.Suffix_VertexChart, format.GetDescription()),
                directory => [@"-i", AssetPath(@"two-scenarios.zpp"), @"--vertex-directory", directory, @"--vertex-format", format.ToString()]);

            new FileInfo(overwrittenFile).Length.ShouldBe(new FileInfo(freshFile).Length);
        }

        [Theory]
        [InlineData(PlotExport.Jpeg)]
        [InlineData(PlotExport.Png)]
        [InlineData(PlotExport.Bmp)]
        [InlineData(PlotExport.Webp)]
        [InlineData(PlotExport.Svg)]
        public async Task Main_Given_PlotExportOverLargerFile_Then_SameLengthAsFreshExport(PlotExport format)
        {
            // Chart images are written by ScottPlot itself, and its
            // File.WriteAllBytes and File.WriteAllText already replace an
            // existing file. Pinned so that a ScottPlot upgrade cannot change
            // that unnoticed.
            (string freshFile, string overwrittenFile) = await ExportFreshThenOverLargerFileAsync(
                directory => Program.BuildExportFilePath(directory, @"two-scenarios", Resource.ProjectPlan.Suffixes.Suffix_GanttChart, format.GetDescription()),
                directory => [@"-i", AssetPath(@"two-scenarios.zpp"), @"--gantt-directory", directory, @"--gantt-size", @"800:600", @"--gantt-format", format.ToString()]);

            new FileInfo(overwrittenFile).Length.ShouldBe(new FileInfo(freshFile).Length);
        }

        [Fact]
        public async Task Main_Given_XlsxExportOverLargerFile_Then_WorkbookOpens()
        {
            (string freshFile, string overwrittenFile) = await ExportFreshThenOverLargerFileAsync(
                directory => Path.Combine(directory, @"two-scenarios.xlsx"),
                directory => [@"-i", AssetPath(@"two-scenarios.zpp"), @"-x", Path.Combine(directory, @"two-scenarios.xlsx")]);

            // A workbook records the time it was created, which can shift its
            // compressed length by a byte from one export to the next, so it
            // cannot be compared by length. Instead it has to open the way zip
            // readers open it, from the central directory at the end of the file,
            // and hold the same parts as the fresh export.
            using ZipArchive fresh = ZipFile.OpenRead(freshFile);
            using ZipArchive overwritten = ZipFile.OpenRead(overwrittenFile);
            overwritten.Entries.Select(x => x.FullName).ShouldBe(fresh.Entries.Select(x => x.FullName));
        }

        [Fact]
        public async Task Main_Given_PlotExportFails_Then_ExitFailure()
        {
            // A failed chart save is reported rather than thrown, so that the run
            // carries on to its other outputs; without a check of its own the run
            // would exit 0 with the chart stale or missing.
            string ganttFile = Program.BuildExportFilePath(m_TempDirectory, @"two-scenarios", Resource.ProjectPlan.Suffixes.Suffix_GanttChart, PlotExport.Png.GetDescription());

            (int exitCode, _) = await RunCapturedWithFileLockedAsync(
                ganttFile,
                @"-i", AssetPath(@"two-scenarios.zpp"), @"--gantt-directory", m_TempDirectory, @"--gantt-size", @"800:600", @"--gantt-format", @"png");

            exitCode.ShouldBe(1);
        }

        [Fact]
        public async Task Main_Given_GraphExportFails_Then_ExitFailure()
        {
            // A failed graph save is reported the same way.
            string vertexFile = Program.BuildExportFilePath(m_TempDirectory, @"two-scenarios", Resource.ProjectPlan.Suffixes.Suffix_VertexChart, GraphExport.Svg.GetDescription());

            (int exitCode, _) = await RunCapturedWithFileLockedAsync(
                vertexFile,
                @"-i", AssetPath(@"two-scenarios.zpp"), @"--vertex-directory", m_TempDirectory, @"--vertex-format", @"svg");

            exitCode.ShouldBe(1);
        }

        [Fact]
        public async Task Main_Given_ExportFails_Then_RemainingOutputsStillProduced()
        {
            // A failed export fails the run without cutting it short: the exports
            // after it still run and the metrics still reach stdout, as the only
            // thing there.
            string ganttFile = Program.BuildExportFilePath(m_TempDirectory, @"two-scenarios", Resource.ProjectPlan.Suffixes.Suffix_GanttChart, PlotExport.Png.GetDescription());
            string vertexFile = Program.BuildExportFilePath(m_TempDirectory, @"two-scenarios", Resource.ProjectPlan.Suffixes.Suffix_VertexChart, GraphExport.Svg.GetDescription());

            (int exitCode, string output) = await RunCapturedWithFileLockedAsync(
                ganttFile,
                @"-i", AssetPath(@"two-scenarios.zpp"), @"--gantt-directory", m_TempDirectory, @"--gantt-size", @"800:600", @"--gantt-format", @"png", @"--vertex-directory", m_TempDirectory, @"--vertex-format", @"svg", @"--metrics-format", @"json");

            exitCode.ShouldBe(1);
            File.Exists(vertexFile).ShouldBeTrue();
            JObject metrics = JObject.Parse(output);
            metrics[@"TotalCost"].ShouldNotBeNull();
        }

        // zpp ends the lines it writes with NewLineHelper.NewLine on every platform, so that a run produces the same bytes
        // on Windows as on Linux. Windows' own line end is "\r\n", which is where these tests have teeth. The help text is
        // the one exception on stdout, because CommandLineParser writes it.
        [Theory]
        [InlineData(@"json")]
        [InlineData(@"table")]
        [InlineData(@"markdown")]
        public async Task Main_Given_MetricsFormat_Then_StdoutLinesEndWithLf(string metricsFormat)
        {
            (int exitCode, string output) = await RunCapturedAsync(@"-i", AssetPath(@"two-scenarios.zpp"), @"--metrics-format", metricsFormat);

            exitCode.ShouldBe(0);
            ShouldEndLinesWithNewLine(output);
        }

        [Fact]
        public async Task Main_Given_ListScenarios_Then_StdoutLinesEndWithLf()
        {
            (int exitCode, string output) = await RunCapturedAsync(@"-i", AssetPath(@"two-scenarios.zpp"), @"--list-scenarios");

            exitCode.ShouldBe(0);
            ShouldEndLinesWithNewLine(output);
        }

        [Fact]
        public async Task Main_Given_BrokenDependencies_Then_StdoutLinesEndWithLf()
        {
            (int exitCode, string output) = await RunCapturedAsync(@"-i", AssetPath(@"broken-dependency.zpp"));

            exitCode.ShouldBe(3);
            ShouldEndLinesWithNewLine(output);
        }

        [Fact]
        public async Task Main_Given_OutputFile_Then_ProjectFileLinesEndWithLf()
        {
            string outputFile = Path.Combine(m_TempDirectory, @"two-scenarios.zpp");

            int exitCode = await Program.Main([@"-i", AssetPath(@"two-scenarios.zpp"), @"-o", outputFile]);

            exitCode.ShouldBe(0);
            string content = File.ReadAllText(outputFile);
            ShouldEndLinesWithNewLine(content);
        }

        [Theory]
        [InlineData(@"arrow", GraphExport.GraphML)]
        [InlineData(@"arrow", GraphExport.Dot)]
        [InlineData(@"vertex", GraphExport.GraphML)]
        [InlineData(@"vertex", GraphExport.Dot)]
        public async Task Main_Given_GraphDataExport_Then_FileLinesEndWithLf(string graph, GraphExport format)
        {
            int exitCode = await Program.Main([@"-i", AssetPath(@"two-scenarios.zpp"), $@"--{graph}-directory", m_TempDirectory, $@"--{graph}-format", format.ToString()]);

            exitCode.ShouldBe(0);
            string content = File.ReadAllText(Directory.GetFiles(m_TempDirectory).ShouldHaveSingleItem());
            ShouldEndLinesWithNewLine(content);
        }

        // A run that cannot go on says why on stderr and prints nothing else: the
        // reason alone, ending with the platform's line end, since zpp leaves
        // stderr's lines as the platform ends them.
        [Fact]
        public async Task Main_Given_InputAndImport_Then_StderrSaysWhy()
        {
            (int exitCode, string output, string error) = await RunCapturedWithErrorAsync(@"-i", AssetPath(@"two-scenarios.zpp"), @"-m", AssetPath(@"two-scenarios.zpp"));

            exitCode.ShouldBe(2);
            output.ShouldBeEmpty();
            error.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_SpecifyEitherOptionNotBoth, @"--input", @"--import") + Environment.NewLine);
        }

        [Fact]
        public async Task Main_Given_UnknownScenario_Then_StderrPointsAtListScenarios()
        {
            (int exitCode, string output, string error) = await RunCapturedWithErrorAsync(@"-i", AssetPath(@"two-scenarios.zpp"), @"-s", @"No Such Scenario");

            exitCode.ShouldBe(1);
            output.ShouldBeEmpty();
            error.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_NoScenarioMatches, @"No Such Scenario", @"--list-scenarios") + Environment.NewLine);
        }

        // zpp serve is zpp's too, with exit codes for its options as zpp has for its own. A server that starts runs until
        // it is stopped, which Main cannot be asked to do, so JobServerTests checks what serve refuses to start with -
        // giving it a time limit, so that a refusal that is lost cannot hold the run up.
        [Fact]
        public async Task Main_Given_ServeHelp_Then_ExitSuccess()
        {
            (int exitCode, string output) = await RunCapturedAsync(@"serve", @"--help");

            exitCode.ShouldBe(0);
            output.ShouldContain(@"--max-jobs");
        }

        [Fact]
        public async Task Main_Given_ServeWithAnUnknownOption_Then_ExitUsageError()
        {
            (int exitCode, _) = await RunCapturedAsync(@"serve", @"--nonsense");

            exitCode.ShouldBe(2);
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
