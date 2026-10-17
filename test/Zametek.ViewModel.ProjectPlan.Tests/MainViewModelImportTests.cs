using Dock.Model.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using static Zametek.ViewModel.ProjectPlan.Tests.XlsxCellSpec;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// What the desktop does when a user imports a workbook: a workbook that has problems is shown to the user and imports
    /// nothing, and the project that is open stays as it is.
    /// </summary>
    public class MainViewModelImportTests
        : IDisposable
    {
        private readonly string m_Directory;
        private readonly ProjectScenarioManagerViewModel m_Project;
        private readonly CoreViewModel m_Core;
        private readonly ISettingService m_Settings;
        private readonly ScriptedDialogService m_Dialogs = new();
        private readonly MainViewModel m_Main;

        public MainViewModelImportTests()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            m_Directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"zpp-import-{Guid.NewGuid():N}")).FullName;
            var dateTimeCalculator = new DateTimeCalculator(TimeProvider.System);

            (m_Core, m_Settings) = CoreViewModelFixture.CreateWithSettingService(
                new ProjectScenarioFileImport(
                    DoNothingProxy.Of<IMicrosoftProjectFileImporter>(),
                    new XlsxScenarioFileImporter(dateTimeCalculator)));
            m_Project = new ProjectScenarioManagerViewModel(
                m_Core,
                m_Settings,
                m_Dialogs,
                dateTimeCalculator,
                CoreViewModelFixture.CreateUIDispatcher());
            m_Main = new MainViewModel(
                DoNothingProxy.Of<IFactory>(),
                DoNothingProxy.Of<IDockSerializer>(),
                DoNothingProxy.Of<IDataGridLayoutManager>(),
                m_Project,
                m_Core,
                DoNothingProxy.Of<IProjectFileOpen>(),
                DoNothingProxy.Of<IProjectFileSave>(),
                m_Settings,
                m_Dialogs,
                DoNothingProxy.Of<IServiceProvider>(),
                NullLogger<MainViewModel>.Instance);

            // A project is open, and a reset would show: it has a name for its scenario that a new project does not have.
            m_Project.ResetProject();
            m_Settings.SetProjectScenarioTitle(@"Alpha");
        }

        public void Dispose()
        {
            m_Project.Dispose();
            m_Core.Dispose();

            try
            {
                Directory.Delete(m_Directory, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup only.
            }
        }

        private string Write(byte[] workbook)
        {
            string filename = Path.Combine(m_Directory, @"plan.xlsx");
            File.WriteAllBytes(filename, workbook);
            return filename;
        }

        private static byte[] WorkbookWithProblems(int count) =>
            new XlsxPackageBuilder()
                .Sheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetActivities, s =>
                {
                    s.Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(nameof(ActivityModel.Duration))));

                    for (int i = 0; i < count; i++)
                    {
                        s.Row(i + 2, ("A", NumberOf(i + 1)), ("B", TextOf(@"soon")));
                    }
                })
                .Build();

        [Fact]
        public async Task ImportProjectScenarioFileAsync_Given_AWorkbookWithProblems_Then_TheyAreShownAndTheOpenProjectStaysAsItIs()
        {
            Guid scenarioId = m_Settings.ScenarioId;
            m_Dialogs.OpenFilename = Write(WorkbookWithProblems(3));

            await m_Main.ImportProjectScenarioFileAsync();

            (string title, string header, string message) = m_Dialogs.Errors.ShouldHaveSingleItem();
            title.ShouldBe(Resource.ProjectPlan.Titles.Title_ImportProblems);
            header.ShouldBe(ImportIssueText.BuildHeader(3));
            message.ShouldContain(@"Activities!B2 (Duration): ""soon"" is not a whole number.");

            // Nothing was imported, and the project was not reset: it is the project that was open.
            m_Settings.ScenarioId.ShouldBe(scenarioId);
            m_Settings.ScenarioTitle.ShouldBe(@"Alpha");
            m_Core.BuildProjectScenario().DependentActivities.ShouldBeEmpty();
            m_Core.IsBusy.ShouldBeFalse();
        }

        [Fact]
        public async Task ImportProjectScenarioFileAsync_Given_ManyProblemsAndAUserWhoWantsTheReport_Then_TheReportIsSaved()
        {
            string report = Path.Combine(m_Directory, @"report.csv");
            m_Dialogs.OpenFilename = Write(WorkbookWithProblems(25));
            m_Dialogs.SaveFilename = report;

            await m_Main.ImportProjectScenarioFileAsync();

            m_Dialogs.Questions.ShouldHaveSingleItem().ShouldBe(@"Do you want to save a report of all 25 problems?");
            m_Dialogs.SaveDialogs.ShouldHaveSingleItem().Name.ShouldBe(@"plan-import-problems.csv");
            File.ReadAllLines(report).Length.ShouldBe(26);   // The titles, and a row for each problem.
            m_Settings.ScenarioTitle.ShouldBe(@"Alpha");
        }

        // A file that is not a workbook at all is a problem of the same kind: it is shown, and the open project stays.
        [Fact]
        public async Task ImportProjectScenarioFileAsync_Given_AFileThatIsNotAWorkbook_Then_ItIsShownAndTheOpenProjectStaysAsItIs()
        {
            m_Dialogs.OpenFilename = Write(System.Text.Encoding.UTF8.GetBytes(@"This is not a workbook."));

            await m_Main.ImportProjectScenarioFileAsync();

            m_Dialogs.Errors.ShouldHaveSingleItem().Header.ShouldBe(ImportIssueText.BuildHeader(1));
            m_Settings.ScenarioTitle.ShouldBe(@"Alpha");
        }

        [Fact]
        public async Task ImportProjectScenarioFileAsync_Given_AWorkbookWithNoProblem_Then_ItIsImported()
        {
            m_Dialogs.OpenFilename = Path.GetFullPath(Path.Combine(@"TestFiles", @"Xlsx", @"sample-days.xlsx"));

            await m_Main.ImportProjectScenarioFileAsync();

            m_Dialogs.Errors.ShouldBeEmpty();
            m_Core.BuildProjectScenario().DependentActivities.ShouldNotBeEmpty();
        }

        [Fact]
        public async Task ImportProjectScenarioFileAsync_Given_ACancelledDialog_Then_NothingHappens()
        {
            m_Dialogs.OpenFilename = null;

            await m_Main.ImportProjectScenarioFileAsync();

            m_Dialogs.Errors.ShouldBeEmpty();
            m_Settings.ScenarioTitle.ShouldBe(@"Alpha");
        }
    }
}
