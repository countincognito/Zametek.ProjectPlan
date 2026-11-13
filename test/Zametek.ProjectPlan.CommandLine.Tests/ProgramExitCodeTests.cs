using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shouldly;
using SkiaSharp;
using System.Diagnostics;
using System.IO.Compression;
using Xunit;
using Zametek.Common.ProjectPlan;
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
    /// in one collection - with ProjectEndpointsParityTests, which run Main as well -
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

        // The example plan of docs/PROJECT-FILE.md: the first JSON block of the page.
        private static string ProjectFileExamplePlan()
        {
            string page = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, @"Docs", @"PROJECT-FILE.md"));
            int start = page.IndexOf("```json\n", StringComparison.Ordinal) + "```json\n".Length;
            return page[start..page.IndexOf("```", start, StringComparison.Ordinal)];
        }

        private static async Task<(int ExitCode, string Output)> RunCapturedAsync(params string[] args)
        {
            TextWriter original = Console.Out;
            try
            {
                using var writer = new StringWriter();
                Console.SetOut(writer);
                int exitCode = await ZppMain.RunAsync(args);
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

            int freshExitCode = await ZppMain.RunAsync(buildArgs(freshDirectory));
            freshExitCode.ShouldBe(0);

            File.WriteAllText(overwrittenFile, new string('X', (int)new FileInfo(freshFile).Length + c_StaleFileExcessLength));

            int overwriteExitCode = await ZppMain.RunAsync(buildArgs(staleDirectory));
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
            int exitCode = await ZppMain.RunAsync([@"-i", AssetPath(@"two-scenarios.zpp")]);

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

            int exitCode = await ZppMain.RunAsync([@"-i", AssetPath(@"two-scenarios.zpp"), @"-s", @"Beta", @"-o", outputFile]);

            exitCode.ShouldBe(0);
            JObject saved = JObject.Parse(File.ReadAllText(outputFile));
            saved[@"Current"]!.ToString().ShouldBe(betaId);
        }

        // A script that compiles each scenario of a plan in turn saves the plan over itself. zpp
        // kept the input open while it wrote the output, and Windows refused the write ("being
        // used by another process"). Linux allows it, so the test fails only on Windows without
        // the fix.
        [Fact]
        public async Task Main_Given_OutputOverTheInput_Then_SavesThePlanOverItself()
        {
            JObject asset = JObject.Parse(File.ReadAllText(AssetPath(@"two-scenarios.zpp")));
            string betaId = asset[@"Nodes"]!
                .First(x => string.Equals((string?)x[@"Name"], @"Beta", StringComparison.Ordinal))[@"Id"]!
                .ToString();
            string planFile = Path.Combine(m_TempDirectory, @"plan.zpp");
            File.Copy(AssetPath(@"two-scenarios.zpp"), planFile);

            int exitCode = await ZppMain.RunAsync([@"-i", planFile, @"-s", @"Beta", @"-o", planFile]);

            exitCode.ShouldBe(0);
            JObject saved = JObject.Parse(File.ReadAllText(planFile));
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

            int exitCode = await ZppMain.RunAsync([@"-i", AssetPath(@"two-scenarios.zpp"), @"-s", betaIdPrefix, @"-o", outputFile]);

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
            (await ZppMain.RunAsync([@"-i", AssetPath(@"two-scenarios.zpp"), @"-x", workbook])).ShouldBe(0);

            int exitCode = await ZppMain.RunAsync([@"-m", workbook, @"-o", outputFile]);

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
            int exitCode = await ZppMain.RunAsync([@"-i", AssetPath(@"two-scenarios.zpp"), @"-s", @"No Such Scenario"]);

            exitCode.ShouldBe(1);
        }

        [Fact]
        public async Task Main_Given_InputAndImport_Then_ExitUsageError()
        {
            int exitCode = await ZppMain.RunAsync([@"-i", AssetPath(@"two-scenarios.zpp"), @"-m", AssetPath(@"two-scenarios.zpp")]);

            exitCode.ShouldBe(2);
        }

        [Fact]
        public async Task Main_Given_ScenarioWithImport_Then_ExitUsageError()
        {
            int exitCode = await ZppMain.RunAsync([@"-m", AssetPath(@"two-scenarios.zpp"), @"-s", @"Beta"]);

            exitCode.ShouldBe(2);
        }

        [Fact]
        public async Task Main_Given_NegativeCompileTimeout_Then_ExitUsageError()
        {
            int exitCode = await ZppMain.RunAsync([@"-i", AssetPath(@"two-scenarios.zpp"), @"--compile-timeout", @"-1"]);

            exitCode.ShouldBe(2);
        }

        [Fact]
        public async Task Main_Given_NowWithoutAnOffset_Then_ExitUsageError()
        {
            int exitCode = await ZppMain.RunAsync([@"-i", AssetPath(@"two-scenarios.zpp"), @"--now", @"2026-09-27T12:00:00"]);

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

            (await ZppMain.RunAsync(Args(first))).ShouldBe(0);
            (await ZppMain.RunAsync(Args(second))).ShouldBe(0);

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
            int exitCode = await ZppMain.RunAsync([@"-i", AssetPath(@"two-scenarios.zpp"), @"--compile-timeout", @"0"]);

            exitCode.ShouldBe(0);
        }

        [Fact]
        public async Task Main_Given_DirectoryWithoutSize_Then_ExitUsageError()
        {
            int exitCode = await ZppMain.RunAsync([@"-i", AssetPath(@"two-scenarios.zpp"), @"--gantt-directory", m_TempDirectory]);

            exitCode.ShouldBe(2);
        }

        [Fact]
        public async Task Main_Given_MissingExportDirectory_Then_ExitFailure()
        {
            string missingDirectory = Path.Combine(m_TempDirectory, @"does-not-exist");

            int exitCode = await ZppMain.RunAsync([@"-i", AssetPath(@"two-scenarios.zpp"), @"--gantt-directory", missingDirectory, @"--gantt-size", @"800:600"]);

            exitCode.ShouldBe(1);
        }

        [Fact]
        public async Task Main_Given_BrokenDependencies_Then_ExitCompilationErrors()
        {
            int exitCode = await ZppMain.RunAsync([@"-i", AssetPath(@"broken-dependency.zpp")]);

            exitCode.ShouldBe(3);
        }

        // A milestone in a phase finishes after the last activity of the phase that takes time, and an indirect resource has
        // the phase. Up to version 4.0.1, the library threw when it marked the phase of the resource, and zpp exited with 1.
        [Fact]
        public async Task Main_Given_AMilestoneInAPhaseAfterItsLastActivity_Then_ExitSuccess()
        {
            int exitCode = await ZppMain.RunAsync([@"-i", AssetPath(@"milestone-after-phase.zpp")]);

            exitCode.ShouldBe(0);
        }

        // A milestone has a maximum LFT that the plan cannot meet. Up to version 4.0.1, the library threw, and zpp exited with
        // 1. The compile now reports the broken constraints, as it does for an activity that takes time.
        [Fact]
        public async Task Main_Given_AMaximumLatestFinishTimeThatAMilestoneCannotMeet_Then_ExitCompilationErrors()
        {
            (int exitCode, string output, string error) = await RunCapturedWithErrorAsync(@"-i", AssetPath(@"unmet-maximum-lft.zpp"));

            exitCode.ShouldBe(3);
            (output + error).ShouldContain(@"C0010");
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
        public async Task Main_Given_Help_Then_ShowsEachWayToRunZppAboveTheOptions()
        {
            (_, string output) = await RunCapturedAsync(@"--help");

            // Set off from the heading by a blank line.
            output.ShouldContain(Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, Options.Usage));
            output.IndexOf(Options.Usage[0], StringComparison.Ordinal).ShouldBeLessThan(output.IndexOf(@"--input", StringComparison.Ordinal));
        }

        // A package manager or a script runs zpp --version to make sure that zpp runs, and takes any exit code but 0 as a
        // failure.
        [Fact]
        public async Task Main_Given_Version_Then_ExitSuccess()
        {
            (int exitCode, _) = await RunCapturedAsync(@"--version");

            exitCode.ShouldBe(0);
        }

        // The heading of the help, which names the version, on a line of its own, and not the help itself. The line ends
        // as each line that zpp writes itself does.
        [Fact]
        public async Task Main_Given_Version_Then_PrintsTheNameAndTheVersionOnly()
        {
            (_, string output) = await RunCapturedAsync(@"--version");

            output.ShouldBe(s_VersionLine + NewLineHelper.NewLine);
        }

        [Fact]
        public async Task Main_Given_Help_Then_ListsVersion()
        {
            (_, string output) = await RunCapturedAsync(@"--help");

            output.ShouldContain(@"--version");
        }

        [Fact]
        public async Task Main_Given_ServeVersion_Then_PrintsTheNameAndTheVersionOnly()
        {
            (int exitCode, string output) = await RunCapturedAsync(@"serve", @"--version");

            exitCode.ShouldBe(0);
            output.ShouldBe(s_VersionLine + NewLineHelper.NewLine);
        }

        // The name and the version of zpp, as the heading of the help gives them.
        private static readonly string s_VersionLine =
            $@"{Resource.ProjectPlan.Labels.Label_CliAppName}, {Resource.ProjectPlan.Labels.Label_Version} {Resource.ProjectPlan.Labels.Label_AppVersion}";

        [Fact]
        public void Usage_Given_EachLine_Then_FitsATerminal80ColumnsWide()
        {
            // The help wraps at 80 columns, so a longer line would come out broken where the library breaks it, not
            // where its text does.
            Options.Usage.Concat(ServeOptions.Usage).ShouldAllBe(line => line.Length <= 80);
        }

        [Fact]
        public async Task Main_Given_UnknownOption_Then_ExitUsageError()
        {
            (int exitCode, _) = await RunCapturedAsync(@"--nonsense");

            exitCode.ShouldBe(2);
        }

        [Fact]
        public async Task Main_Given_UnknownOption_Then_SaysWhatIsWrongBeforeHowZppIsUsed()
        {
            (_, string output) = await RunCapturedAsync(@"--nonsense");

            int error = output.IndexOf(@"Option 'nonsense' is unknown.", StringComparison.Ordinal);
            int usage = output.IndexOf(Options.Usage[0], StringComparison.Ordinal);

            error.ShouldBeGreaterThan(0);
            usage.ShouldBeGreaterThan(error);
            output.IndexOf(@"--input", StringComparison.Ordinal).ShouldBeGreaterThan(usage);
        }

        [Fact]
        public async Task Main_Given_AnOptionWithoutItsValue_Then_ExitUsageErrorBeforeTheRun()
        {
            // The parser would drop the -o at the end, and the run would go ahead, saving nothing.
            string export = Path.Combine(m_TempDirectory, @"plan.xlsx");

            (int exitCode, string output, string error) = await RunCapturedWithErrorAsync(@"-i", AssetPath(@"two-scenarios.zpp"), @"-x", export, @"-o");

            exitCode.ShouldBe(2);
            error.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_OptionNeedsValue, @"--output") + Environment.NewLine);
            output.ShouldBeEmpty();
            File.Exists(export).ShouldBeFalse();
        }

        [Fact]
        public async Task Main_Given_AShortOptionsValueAfterAnEquals_Then_TakesTheValueWithoutIt()
        {
            // The parser would keep the =, and save the plan somewhere else, if anywhere.
            string saved = Path.Combine(m_TempDirectory, @"plan.zpp");

            int exitCode = await ZppMain.RunAsync([@"-i", AssetPath(@"two-scenarios.zpp"), $@"-o={saved}"]);

            exitCode.ShouldBe(0);
            File.Exists(saved).ShouldBeTrue();
        }

        [Fact]
        public async Task Main_Given_AWordForNothing_Then_ExitUsageErrorNamingIt()
        {
            (int exitCode, string output, string error) = await RunCapturedWithErrorAsync(@"-i", AssetPath(@"two-scenarios.zpp"), @"extra");

            exitCode.ShouldBe(2);
            error.ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_ArgumentNotOptionOrValue, @"extra", @"zpp --help") + Environment.NewLine);
            output.ShouldBeEmpty();
        }

        [Fact]
        public async Task Main_Given_NoArguments_Then_ExitUsageError()
        {
            (int exitCode, _) = await RunCapturedAsync();

            exitCode.ShouldBe(2);
        }

        [Fact]
        public async Task Main_Given_APlanThatStartsOnASaturdayWithWeekends_Then_ItsWorkStartsOnTheMondayAfter()
        {
            // The plan asks to start on Saturday 6 January 2024. With weekends, its work starts on Monday 8 January, which is
            // day 0. zpp gives the start in the metrics, and the file keeps the start that the plan asks for beside it.
            JObject plan = JObject.Parse(File.ReadAllText(AssetPath(@"two-scenarios.zpp")));
            foreach (JToken scenario in plan.SelectTokens(@"$.Files[*].Scenario"))
            {
                scenario[@"ProjectStart"] = @"2024-01-06T00:00:00+00:00";
                scenario[@"DisplaySettings"]![@"NonWorkingDayMode"] = 1;
            }
            string planFile = Path.Combine(m_TempDirectory, @"saturday.zpp");
            File.WriteAllText(planFile, plan.ToString());
            string outputFile = Path.Combine(m_TempDirectory, @"saturday-saved.zpp");

            (int exitCode, string output) = await RunCapturedAsync(@"-i", planFile, @"-o", outputFile, @"--metrics-format", @"json");

            exitCode.ShouldBe(0);
            output.ShouldContain(@"""ProjectStartDate"": ""2024-01-08""");
            output.ShouldContain(@"""NetworkDuration"": 5");
            using var reader = new JsonTextReader(new StringReader(File.ReadAllText(outputFile))) { DateParseHandling = DateParseHandling.None };
            JToken saved = JObject.Load(reader).SelectToken(@"$.Files[0].Scenario")!;
            saved[@"ProjectStart"]!.Value<string>().ShouldStartWith(@"2024-01-06T00:00:00");
            saved[@"EffectiveProjectStart"]!.Value<string>().ShouldStartWith(@"2024-01-08T00:00:00");
        }

        [Fact]
        public async Task Main_Given_APlanThatTheCheckBeforeTheLoadRefuses_Then_ExitCode1WithEachProblemOnStandardError()
        {
            // The load of this plan failed with an exception (B8 of the stress test of 0.11.0-beta.1).
            JObject plan = JObject.Parse(File.ReadAllText(AssetPath(@"two-scenarios.zpp")));
            plan[@"Files"]![0]![@"Scenario"]![@"DisplaySettings"]![@"NonWorkingDayMode"] = 7;
            string planFile = Path.Combine(m_TempDirectory, @"mode-7.zpp");
            File.WriteAllText(planFile, plan.ToString());

            (int exitCode, _, string error) = await RunCapturedWithErrorAsync(@"-i", planFile);

            exitCode.ShouldBe(1);
            error.ShouldContain(@"/Files/0/Scenario/DisplaySettings/NonWorkingDayMode: must be one of: 0 (None), 1 (Weekends), 2 (CustomCalendar)");
        }

        [Fact]
        public async Task Main_Given_ATargetResourceThatNoResourceHas_Then_AWarningOnStandardErrorAndExitCode0()
        {
            JObject plan = JObject.Parse(File.ReadAllText(AssetPath(@"two-scenarios.zpp")));
            plan[@"Files"]![0]![@"Scenario"]![@"DependentActivities"]![0]![@"Activity"]![@"TargetResources"] = new JArray(99);
            string planFile = Path.Combine(m_TempDirectory, @"target-99.zpp");
            File.WriteAllText(planFile, plan.ToString());

            (int exitCode, _, string error) = await RunCapturedWithErrorAsync(@"-i", planFile);

            exitCode.ShouldBe(0);
            error.ShouldContain(@"Warning: /Files/0/Scenario/DependentActivities/0/Activity/TargetResources/0: is not the Id of a resource of the scenario");
        }

        [Fact]
        public async Task Main_Given_CompileTrackedAndATrackedScenarioThatDoesNotCompile_Then_ItsErrorsOnStandardErrorAndExitCode0()
        {
            JObject plan = JObject.Parse(File.ReadAllText(AssetPath(@"stale-tracked.zpp")));
            plan[@"Files"]![1]![@"Scenario"]![@"DependentActivities"]![0]![@"Dependencies"] = new JArray(42);
            string planFile = Path.Combine(m_TempDirectory, @"broken-beta.zpp");
            File.WriteAllText(planFile, plan.ToString());

            (int exitCode, _, string error) = await RunCapturedWithErrorAsync(@"-i", planFile, @"--compile-tracked");

            exitCode.ShouldBe(0);
            error.ShouldContain(@"The tracked scenario 'Beta' did not compile:");
            error.ShouldContain(@"P0010: ");
        }

        [Fact]
        public async Task Main_Given_CompileTracked_Then_PrintsTheMetricsOfEachTrackedScenario()
        {
            (int exitCode, string output) = await RunCapturedAsync(
                @"-i", AssetPath(@"stale-tracked.zpp"), @"--compile-tracked", @"--metrics-format", @"json");

            exitCode.ShouldBe(0);

            // Beta's file holds stale metrics, with no duration. Its compile gives it the duration of its task.
            JArray tracked = (JArray)JObject.Parse(output)[@"TrackedScenarios"]!;
            tracked.Select(x => (x[@"Path"]!.Value<string>(), x[@"IsCurrent"]!.Value<bool>(), x[@"Metrics"]![@"NetworkDuration"]!.Value<int>()))
                .ShouldBe([(@"Alpha", true, 5), (@"Beta", false, 10)]);
        }

        [Fact]
        public async Task Main_Given_TheExampleOfTheRulesOfTheProjectFile_Then_TheScheduleIsWhatThePageSays()
        {
            // docs/PROJECT-FILE.md gives a plan that a program can write, and says what zpp makes of it: the first activity has
            // its most likely duration of 5 days, the second has only a Duration, which is its most likely duration of 3 days,
            // and the third has the three estimates 2, 4 and 12, which give (2 + 4 × 4 + 12 + 3) / 6 = 5 days in whole days. In a
            // chain, they finish on day 13.
            string plan = ProjectFileExamplePlan();

            // The page also says that zpp calculates the duration and the times again: a stale Duration beside a most likely
            // duration, and a stale earliest start, change nothing.
            JObject stale = JObject.Parse(plan);
            JToken activities = stale[@"Files"]![0]![@"Scenario"]![@"DependentActivities"]!;
            activities[0]![@"Activity"]![@"Duration"] = 99;
            activities[1]![@"Activity"]![@"EarliestStartTime"] = 40;

            foreach (string text in new[] { plan, stale.ToString() })
            {
                string directory = Path.Combine(Path.GetTempPath(), $@"zpp-rules-{Guid.NewGuid():N}");
                Directory.CreateDirectory(directory);
                try
                {
                    string input = Path.Combine(directory, @"plan.zpp");
                    File.WriteAllText(input, text);

                    (int exitCode, string output) = await RunCapturedAsync(@"-i", input, @"--data-directory", directory, @"--metrics-format", @"json");

                    exitCode.ShouldBe(0);
                    JObject.Parse(output)[@"NetworkDuration"]!.Value<int>().ShouldBe(13);

                    string[][] schedule = [.. File.ReadAllLines(Path.Combine(directory, @"plan-schedule.csv")).Skip(1).Select(x => x.Split(','))];
                    schedule.Select(x => (x[0], x[2], x[3])).ShouldBe([(@"1", @"5", @"0"), (@"2", @"3", @"5"), (@"3", @"5", @"8")]);
                }
                finally
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }

        [Fact]
        public async Task Main_Given_ConstraintsWithOnlyTheirNumbers_Then_AWeekendCalendarGivesTheSameScheduleInDays()
        {
            // A time of an activity is a number of working days from ProjectStart (docs/PROJECT-FILE.md). So, when a plan gives
            // its constraints only as numbers, its schedule in days is the same with weekends as with no non-working days. zpp
            // starts each load from the default calendar None. When the calendar of the plan came after the activities, each
            // number went to a date along every day, and came back along the working days. With weekends, the minimum earliest
            // start time of 40 became 30, and the maximum latest finish time of 50 became 36, before Test finished on day 38:
            // the compilation failed with C0010.
            JObject template = JObject.Parse(ProjectFileExamplePlan());
            JToken activities = template[@"Files"]![0]![@"Scenario"]![@"DependentActivities"]!;
            activities[1]![@"Activity"]![@"MinimumEarliestStartTime"] = 40;
            activities[2]![@"Activity"]![@"MaximumLatestFinishTime"] = 50;

            var schedules = new List<string[][]>();
            foreach (NonWorkingDayMode mode in new[] { NonWorkingDayMode.None, NonWorkingDayMode.Weekends })
            {
                JObject plan = (JObject)template.DeepClone();
                plan[@"Files"]![0]![@"Scenario"]![@"DisplaySettings"] = new JObject { [@"NonWorkingDayMode"] = (int)mode };

                string directory = Path.Combine(m_TempDirectory, mode.ToString());
                Directory.CreateDirectory(directory);
                string input = Path.Combine(directory, @"plan.zpp");
                File.WriteAllText(input, plan.ToString());

                (int exitCode, _) = await RunCapturedAsync(@"-i", input, @"--data-directory", directory);

                exitCode.ShouldBe(0);
                schedules.Add([.. File.ReadAllLines(Path.Combine(directory, @"plan-schedule.csv")).Skip(1).Select(x => x.Split(','))]);
            }

            // Id, EarliestStartTime, LatestFinishTime, MinimumEarliestStartTime and MaximumLatestFinishTime. Build waits for
            // day 40, and Test follows it, to finish on day 48, before its maximum latest finish time of day 50.
            schedules[0].Select(x => (x[0], x[3], x[6], x[12], x[13])).ShouldBe(
                [(@"1", @"0", @"40", @"", @""), (@"2", @"40", @"43", @"40", @""), (@"3", @"43", @"48", @"", @"50")]);
            schedules[1].ShouldBe(schedules[0]);
        }

        [Fact]
        public async Task Main_Given_AScenarioChartWithFittedCurvesAsSvg_Then_SkiaWritesNoWarningToStandardError()
        {
            // ScottPlot draws every plottable with one paint, and a dashed fitted curve left its dash on that paint. The markers
            // of the second axis come after the curve of the first one, and the fill of the first marker carried the dash.
            // Skia's SVG canvas then wrote "Unsupported path effect in addPaint." to the standard error of the process, from
            // native code. Console.Error cannot see that stream, so the test runs zpp in a process of its own.
            string directory = Path.Combine(m_TempDirectory, @"scenario-chart");
            Directory.CreateDirectory(directory);

            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable(@"DOTNET_HOST_PATH") ?? @"dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (string argument in new[]
            {
                Path.Combine(AppContext.BaseDirectory, @"zpp.dll"),
                @"-i", AssetPath(@"scenario-chart.zpp"),
                @"--scenario-chart-directory", directory,
                @"--scenario-chart-format", @"Svg",
                @"--scenario-chart-size", @"1200:800",
            })
            {
                start.ArgumentList.Add(argument);
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            using Process process = Process.Start(start).ShouldNotBeNull();
            Task<string> output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            Task<string> error = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }

            await output;
            string errors = await error;
            process.ExitCode.ShouldBe(0, errors);
            Directory.GetFiles(directory, @"*.svg").ShouldNotBeEmpty();
            errors.ShouldNotContain(@"Unsupported path effect");
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
        public async Task Main_Given_GraphsTooLargeForOneImage_Then_ImagesFitTheLimit()
        {
            // At the scale of 2, the graphs of the largest sample plan are far too large for one image. zpp draws each of
            // them at a smaller scale, so that the image has at most 67,108,864 pixels (8,192 x 8,192), and no side longer
            // than 65,500 pixels. Before, zpp wrote empty files and exited with 0.
            int exitCode = await ZppMain.RunAsync([@"-i", AssetPath(@"erp-migration.zpp"), @"--arrow-directory", m_TempDirectory, @"--arrow-format", @"png", @"--vertex-directory", m_TempDirectory, @"--vertex-format", @"jpeg"]);

            exitCode.ShouldBe(0);
            string[] files = Directory.GetFiles(m_TempDirectory);
            files.Length.ShouldBe(2);
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                byte[] data = File.ReadAllBytes(file);
                data.ShouldNotBeEmpty($@"{name} holds an image");
                SKImageInfo image = SKBitmap.DecodeBounds(data);
                image.Width.ShouldBeGreaterThan(0, $@"{name} decodes");
                ((long)image.Width * image.Height).ShouldBeLessThanOrEqualTo(8_192L * 8_192L, name);
                Math.Max(image.Width, image.Height).ShouldBeLessThanOrEqualTo(65_500, name);
            }
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

            int exitCode = await ZppMain.RunAsync([@"-i", AssetPath(@"two-scenarios.zpp"), @"-o", outputFile]);

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
            int exitCode = await ZppMain.RunAsync([@"-i", AssetPath(@"two-scenarios.zpp"), $@"--{graph}-directory", m_TempDirectory, $@"--{graph}-format", format.ToString()]);

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
        public async Task Main_Given_ServeHelp_Then_SaysWhatServeIsAboveItsOptions()
        {
            (_, string output) = await RunCapturedAsync(@"serve", @"--help");

            output.ShouldContain(string.Join(Environment.NewLine, ServeOptions.Usage));
            output.IndexOf(ServeOptions.Usage[0], StringComparison.Ordinal).ShouldBeLessThan(output.IndexOf(@"--listen", StringComparison.Ordinal));
            output.ShouldNotContain(Options.Usage[1]);
        }

        [Theory]
        [InlineData(@"Serve")]
        [InlineData(@"SERVE")]
        public async Task Main_Given_ServeInCapitals_Then_RunsServe(string serve)
        {
            (int exitCode, string output) = await RunCapturedAsync(serve, @"--help");

            exitCode.ShouldBe(0);
            output.ShouldContain(string.Join(Environment.NewLine, ServeOptions.Usage));
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
