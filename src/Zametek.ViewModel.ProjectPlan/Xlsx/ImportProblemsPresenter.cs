using System.Globalization;
using System.Text;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// What the desktop shows when the import of a workbook is cancelled because the workbook has problems: an error that says
    /// how many there are and tells the first of them, and, when there are more than that, the offer to save a report of all of
    /// them as a CSV file that can be sorted and filtered in a spreadsheet.
    /// </summary>
    /// <remarks>
    /// Nothing of the open project has changed when this is shown, because the import fails before it changes anything, and
    /// thus the project is not reset as it is for a failure that leaves it half loaded.
    /// </remarks>
    public static class ImportProblemsPresenter
    {
        private static readonly IList<IFileFilter> s_CsvFileFilters =
            [
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_CsvFileType,
                    Patterns = [Resource.ProjectPlan.Filters.Filter_CsvFilePattern]
                }
            ];

        /// <summary>
        /// Tells the user what is wrong with the file and, if the error could not tell all of it, offers the report.
        /// </summary>
        /// <param name="dialogService">The dialogs of the application.</param>
        /// <param name="exception">The problems.</param>
        /// <param name="importFilename">The file that was imported. The report is named after it.</param>
        /// <param name="directory">The folder where the dialog that saves the report opens.</param>
        public static async Task ShowAsync(
            IDialogService dialogService,
            ScenarioImportException exception,
            string importFilename,
            string directory)
        {
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(exception);

            await dialogService.ShowErrorAsync(
                Resource.ProjectPlan.Titles.Title_ImportProblems,
                ImportIssueText.BuildHeader(exception.TotalCount),
                ImportIssueText.BuildLines(exception.Issues, exception.TotalCount));

            // A report is offered only when it has more than the error has said.
            if (exception.Issues.Count <= ImportIssueText.SummaryLimit)
            {
                return;
            }

            try
            {
                bool wantsReport = await dialogService.ShowConfirmationAsync(
                    Resource.ProjectPlan.Titles.Title_ImportProblems,
                    string.Empty,
                    string.Format(CultureInfo.CurrentCulture, Resource.ProjectPlan.Messages.Message_ImportSaveReport, exception.Issues.Count));

                if (!wantsReport)
                {
                    return;
                }

                string name = $@"{Path.GetFileNameWithoutExtension(importFilename)}{Resource.ProjectPlan.Suffixes.Suffix_ImportProblems}.csv";
                string? filename = await dialogService.ShowSaveFileDialogAsync(name, directory, s_CsvFileFilters);

                if (string.IsNullOrWhiteSpace(filename))
                {
                    return;
                }

                await Task.Run(() => SaveReport(filename, exception.Issues));
            }
            catch (Exception ex)
            {
                await dialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        /// <summary>
        /// Saves the report of the problems. A byte order mark leads, so that a spreadsheet reads the names as UTF-8.
        /// </summary>
        public static void SaveReport(
            string filename,
            IEnumerable<ImportIssue> issues)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filename);
            ArgumentNullException.ThrowIfNull(issues);

            FileStreamHelper.Save(
                filename,
                stream =>
                {
                    using var writer = new StreamWriter(stream, new UTF8Encoding(true), leaveOpen: true);
                    ImportIssueText.WriteReport(writer, issues);
                });
        }
    }
}
