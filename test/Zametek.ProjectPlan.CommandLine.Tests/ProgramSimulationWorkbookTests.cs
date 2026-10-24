using Shouldly;
using System.IO.Compression;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// zpp writes the result of the simulation as one Excel workbook with --simulation-workbook-directory. The workbook is independent
    /// of the chart and of the CSV files, which another option asks for. The tests run Main, and thus they are in its collection.
    /// </summary>
    [Collection(ProgramExitCodeTests.CollectionName)]
    public class ProgramSimulationWorkbookTests
        : IClassFixture<EngineFixture>, IDisposable
    {
        private readonly EngineFixture m_Engine;
        private readonly string m_TempDirectory;

        public ProgramSimulationWorkbookTests(EngineFixture engine)
        {
            m_Engine = engine;
            m_TempDirectory = Path.Combine(Path.GetTempPath(), $@"zpp-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(m_TempDirectory);
        }

        public void Dispose()
        {
            Directory.Delete(m_TempDirectory, recursive: true);
        }

        private static string Estimated => Path.Combine(AppContext.BaseDirectory, @"Assets", @"estimated.zpp");

        private static string[] Sheets(string workbook)
        {
            using ZipArchive archive = ZipFile.OpenRead(workbook);
            using Stream entry = archive.GetEntry(@"xl/workbook.xml")!.Open();
            return [.. System.Xml.Linq.XDocument.Load(entry).Descendants().Where(x => x.Name.LocalName == @"sheet").Select(x => (string)x.Attribute(@"name")!)];
        }

        [Fact]
        public async Task Main_Given_ARunAndTheDirectoryOfTheWorkbook_Then_WritesOnlyTheWorkbookWithTheSheetOfTheIterations()
        {
            string output = Path.Combine(m_TempDirectory, @"out");
            Directory.CreateDirectory(output);

            (int exitCode, _, string error) = await ZppMain.RunCapturedAsync([@"-i", Estimated, @"--run-simulation", @"--simulation-workbook-directory", output]);

            exitCode.ShouldBe((int)ExitCode.Success, error);
            string workbook = Directory.GetFiles(output).ShouldHaveSingleItem();
            Path.GetFileName(workbook).ShouldBe(@"estimated-simulation.xlsx");
            Sheets(workbook).ShouldContain(@"Simulation Iterations");
            Sheets(workbook).Length.ShouldBe(7);
        }

        [Fact]
        public async Task Main_Given_AProjectThatHoldsAResultAndTheDirectoryOfTheWorkbook_Then_WritesTheWorkbookWithoutTheIterations()
        {
            // The project is saved by a run, and zpp opens it without one.
            string saved = Path.Combine(m_TempDirectory, @"saved.zpp");
            (int savedExitCode, _, string savedError) = await ZppMain.RunCapturedAsync([@"-i", Estimated, @"--run-simulation", @"-o", saved]);
            savedExitCode.ShouldBe((int)ExitCode.Success, savedError);

            string output = Path.Combine(m_TempDirectory, @"out");
            Directory.CreateDirectory(output);
            (int exitCode, _, string error) = await ZppMain.RunCapturedAsync([@"-i", saved, @"--simulation-workbook-directory", output]);

            exitCode.ShouldBe((int)ExitCode.Success, error);
            string[] sheets = Sheets(Directory.GetFiles(output).ShouldHaveSingleItem());
            sheets.ShouldNotContain(@"Simulation Iterations");
            sheets.Length.ShouldBe(6);
        }

        [Fact]
        public async Task Main_Given_TheWorkbookWithTheChartAndTheCsvFiles_Then_WritesEachInItsOwnDirectory()
        {
            string workbooks = Path.Combine(m_TempDirectory, @"workbooks");
            string files = Path.Combine(m_TempDirectory, @"files");
            Directory.CreateDirectory(workbooks);
            Directory.CreateDirectory(files);

            (int exitCode, _, string error) = await ZppMain.RunCapturedAsync(
            [
                @"-i", Estimated, @"--run-simulation",
                @"--simulation-workbook-directory", workbooks,
                @"--simulation-directory", files, @"--simulation-format", @"svg", @"--simulation-size", @"640:480",
            ]);

            exitCode.ShouldBe((int)ExitCode.Success, error);
            Directory.GetFiles(workbooks).Select(Path.GetFileName).ShouldBe([@"estimated-simulation.xlsx"]);
            Directory.GetFiles(files).Select(Path.GetFileName).Order().ShouldBe(
            [
                @"estimated-simulation-activities.csv",
                @"estimated-simulation-histogram.csv",
                @"estimated-simulation-iterations.csv",
                @"estimated-simulation-paths.csv",
                @"estimated-simulation-statistics.csv",
                @"estimated-simulation.svg",
            ]);
        }

        [Fact]
        public async Task Main_Given_TheWorkbookAndNeitherARunNorAResult_Then_FailsAndWritesNothing()
        {
            string output = Path.Combine(m_TempDirectory, @"out");
            Directory.CreateDirectory(output);

            (int exitCode, _, string error) = await ZppMain.RunCapturedAsync([@"-i", Estimated, @"--simulation-workbook-directory", output]);

            exitCode.ShouldBe((int)ExitCode.Failure);
            error.ShouldContain(Resource.ProjectPlan.Messages.Message_SimulationOutputsNeedAResult);
            Directory.GetFiles(output).ShouldBeEmpty();
        }

        [Fact]
        public async Task Main_Given_ADirectoryOfTheWorkbookThatIsNotThere_Then_FailsBeforeAnythingIsDone()
        {
            string missing = Path.Combine(m_TempDirectory, @"missing");

            (int exitCode, string output, string error) = await ZppMain.RunCapturedAsync([@"-i", Estimated, @"--run-simulation", @"--simulation-workbook-directory", missing]);

            exitCode.ShouldNotBe((int)ExitCode.Success);
            output.ShouldBeEmpty();
            error.ShouldContain(string.Format(Resource.ProjectPlan.Messages.Message_DirectoryDoesNotExist, missing));
        }
    }
}
