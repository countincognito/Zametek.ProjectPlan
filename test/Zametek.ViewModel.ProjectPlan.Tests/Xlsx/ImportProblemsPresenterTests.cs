using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// What the desktop shows when the import of a workbook is cancelled because the workbook has problems.
    /// </summary>
    public class ImportProblemsPresenterTests
    {
        private static ScenarioImportException Failure(int count) =>
            new(
                ImportIssueText.BuildMessage(Issues(count), count),
                Issues(count),
                count);

        private static List<ImportIssue> Issues(int count) =>
            [
                .. Enumerable.Range(2, count).Select(x => new ImportIssue
                {
                    Code = ImportIssueCodes.NotAWholeNumber,
                    Message = @"""x"" is not a whole number.",
                    Sheet = @"Activities",
                    Row = x,
                    Column = 2,
                    ColumnTitle = @"Duration",
                    Value = @"x",
                }),
            ];

        [Fact]
        public async Task ShowAsync_Given_FewProblems_Then_ThereIsOneErrorWithAllOfThemAndNoOffer()
        {
            var dialogs = new ScriptedDialogService();
            ScenarioImportException failure = Failure(3);

            await ImportProblemsPresenter.ShowAsync(dialogs, failure, @"plan.xlsx", @"folder");

            (string title, string header, string message) = dialogs.Errors.Single();
            title.ShouldBe(Resource.ProjectPlan.Titles.Title_ImportProblems);
            header.ShouldBe(ImportIssueText.BuildHeader(3));
            message.ShouldBe(ImportIssueText.BuildLines(failure.Issues, 3));
            dialogs.Questions.ShouldBeEmpty();
            dialogs.SaveDialogs.ShouldBeEmpty();
        }

        [Fact]
        public async Task ShowAsync_Given_AsManyProblemsAsTheErrorTells_Then_ThereIsNoOffer()
        {
            var dialogs = new ScriptedDialogService();

            await ImportProblemsPresenter.ShowAsync(dialogs, Failure(ImportIssueText.SummaryLimit), @"plan.xlsx", @"folder");

            dialogs.Questions.ShouldBeEmpty();
        }

        [Fact]
        public async Task ShowAsync_Given_MoreProblemsThanTheErrorTells_Then_ItOffersTheReportAndTheUserDeclines()
        {
            var dialogs = new ScriptedDialogService { WantsReport = false };

            await ImportProblemsPresenter.ShowAsync(dialogs, Failure(40), @"plan.xlsx", @"folder");

            dialogs.Errors.Count.ShouldBe(1);
            dialogs.Errors[0].Message.ShouldEndWith(@"And 30 more.");
            dialogs.Questions.Single().ShouldBe(@"Do you want to save a report of all 40 problems?");
            dialogs.SaveDialogs.ShouldBeEmpty();
        }

        [Fact]
        public async Task ShowAsync_Given_TheUserWantsTheReport_Then_TheDialogIsNamedAfterTheWorkbookAndTheReportHasEveryProblem()
        {
            string filename = Path.Combine(Path.GetTempPath(), $"report-{Guid.NewGuid():N}.csv");
            var dialogs = new ScriptedDialogService { SaveFilename = filename };

            try
            {
                await ImportProblemsPresenter.ShowAsync(dialogs, Failure(40), Path.Combine(@"somewhere", @"plan.xlsx"), @"folder");

                (string name, string directory, IList<IFileFilter> filters) = dialogs.SaveDialogs.Single();
                name.ShouldBe(@"plan-import-problems.csv");
                directory.ShouldBe(@"folder");
                filters.Single().Patterns.ShouldBe([Resource.ProjectPlan.Filters.Filter_CsvFilePattern]);

                byte[] bytes = await File.ReadAllBytesAsync(filename);
                bytes.Take(3).ShouldBe([(byte)0xEF, (byte)0xBB, (byte)0xBF]);   // A byte order mark, so that a spreadsheet reads UTF-8.
                string[] lines = System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).Split('\n', StringSplitOptions.RemoveEmptyEntries);
                lines.Length.ShouldBe(41);
                lines[1].ShouldStartWith(@"Activities,B2,2,2,Duration,,x,notAWholeNumber,");
                dialogs.Errors.Count.ShouldBe(1);
            }
            finally
            {
                File.Delete(filename);
            }
        }

        [Fact]
        public async Task ShowAsync_Given_TheUserCancelsTheSaveDialog_Then_NothingIsWritten()
        {
            var dialogs = new ScriptedDialogService { SaveFilename = null };

            await ImportProblemsPresenter.ShowAsync(dialogs, Failure(40), @"plan.xlsx", @"folder");

            dialogs.SaveDialogs.Count.ShouldBe(1);
            dialogs.Errors.Count.ShouldBe(1);
        }

        [Fact]
        public async Task ShowAsync_Given_AReportThatCannotBeSaved_Then_ThatIsAnErrorOfItsOwn()
        {
            // A name in a folder that does not exist.
            string filename = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}", @"report.csv");
            var dialogs = new ScriptedDialogService { SaveFilename = filename };

            await ImportProblemsPresenter.ShowAsync(dialogs, Failure(40), @"plan.xlsx", @"folder");

            dialogs.Errors.Count.ShouldBe(2);
            dialogs.Errors[1].Title.ShouldBe(Resource.ProjectPlan.Titles.Title_Error);
            File.Exists(filename).ShouldBeFalse();
        }
    }
}
