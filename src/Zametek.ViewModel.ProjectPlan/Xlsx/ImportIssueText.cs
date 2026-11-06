using System.Buffers;
using System.Globalization;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// The words of the problems that an import finds: the sentence for each kind of problem, one line for each problem as the
    /// message of the exception and the command line say it, and the report that a person can save and filter in a
    /// spreadsheet.
    /// </summary>
    public static class ImportIssueText
    {
        /// <summary>
        /// The number of problems that a message tells one by one before it says how many more there are.
        /// </summary>
        public const int SummaryLimit = 10;

        private static readonly SearchValues<char> s_QuotedCharacters = SearchValues.Create(",\"\r\n");

        // A field that starts with one of these is a formula to a spreadsheet that opens the report. The report holds text that
        // came from the cells of a file, and thus text that anyone could have written.
        private static readonly SearchValues<char> s_FormulaCharacters = SearchValues.Create("=+-@\t\r");

        #region The sentences

        internal static string NotAWholeNumber(string value) => Say(Resource.ProjectPlan.Messages.Message_ImportNotAWholeNumber, value);

        internal static string NotANumber(string value) => Say(Resource.ProjectPlan.Messages.Message_ImportNotANumber, value);

        internal static string NotTrueOrFalse(string value) => Say(Resource.ProjectPlan.Messages.Message_ImportNotTrueOrFalse, value);

        internal static string NotADate(string value) => Say(Resource.ProjectPlan.Messages.Message_ImportNotADate, value);

        internal static string NotAChoice(string value, string choices) => Say(Resource.ProjectPlan.Messages.Message_ImportNotAChoice, value, choices);

        internal static string NotAColor(string value) => Say(Resource.ProjectPlan.Messages.Message_ImportNotAColor, value);

        internal static string ExcelError(string value) => Say(Resource.ProjectPlan.Messages.Message_ImportExcelError, value);

        internal static string MissingValue() => Resource.ProjectPlan.Messages.Message_ImportMissingValue;

        internal static string DuplicateId(int id, int row) => Say(Resource.ProjectPlan.Messages.Message_ImportDuplicateId, id, row);

        internal static string UnknownActivity(int id) => Say(Resource.ProjectPlan.Messages.Message_ImportUnknownActivity, id);

        internal static string MissingColumn(string title) => Say(Resource.ProjectPlan.Messages.Message_ImportMissingColumn, title);

        internal static string DuplicateColumn(string title, string column) => Say(Resource.ProjectPlan.Messages.Message_ImportDuplicateColumn, title, column);

        internal static string MissingHeader() => Resource.ProjectPlan.Messages.Message_ImportMissingHeader;

        internal static string NoKnownSheet() => Resource.ProjectPlan.Messages.Message_ImportNoKnownSheet;

        internal static string UnreadableWorkbook(string reason) => Say(Resource.ProjectPlan.Messages.Message_ImportUnreadableWorkbook, reason).TrimEnd();

        private static string Say(string template, params object[] arguments) =>
            string.Format(CultureInfo.CurrentCulture, template, arguments);

        #endregion

        #region The lines

        /// <summary>
        /// One problem in one line: where it is, and what is wrong. For example: Activities!E14 (Duration, Design the wall):
        /// "abc" is not a whole number.
        /// </summary>
        public static string Describe(ImportIssue issue)
        {
            ArgumentNullException.ThrowIfNull(issue);
            string place = Place(issue);
            List<string> details = [];

            if (!string.IsNullOrWhiteSpace(issue.ColumnTitle))
            {
                details.Add(issue.ColumnTitle);
            }

            if (!string.IsNullOrWhiteSpace(issue.RowName))
            {
                details.Add(issue.RowName);
            }

            if (place.Length == 0)
            {
                return issue.Message;
            }

            return details.Count > 0
                ? $@"{place} ({string.Join(@", ", details)}): {issue.Message}"
                : $@"{place}: {issue.Message}";
        }

        // The address as Excel writes it: Activities!E14, with quotes around a name that is not a plain word
        // ('Resource Tracker (2)'!C5).
        private static string Place(ImportIssue issue)
        {
            if (string.IsNullOrEmpty(issue.Sheet))
            {
                return string.Empty;
            }

            string sheet = issue.Sheet.All(x => char.IsLetterOrDigit(x) || x == '_')
                ? issue.Sheet
                : $@"'{issue.Sheet.Replace(@"'", @"''", StringComparison.Ordinal)}'";

            if (issue.Cell is string cell)
            {
                return $@"{sheet}!{cell}";
            }

            return issue.Row is int row
                ? $@"{sheet}!{row}:{row}"
                : sheet;
        }

        /// <summary>
        /// The first problems, one in each line, and how many more there are.
        /// </summary>
        public static string BuildLines(
            IReadOnlyList<ImportIssue> issues,
            int total,
            int limit = SummaryLimit) =>
            string.Join(NewLineHelper.NewLine, ListLines(issues, total, limit));

        /// <summary>
        /// The same lines, each as a string of its own, for a host that writes a line at a time (the command line).
        /// </summary>
        public static IReadOnlyList<string> ListLines(
            IReadOnlyList<ImportIssue> issues,
            int total,
            int limit = SummaryLimit)
        {
            ArgumentNullException.ThrowIfNull(issues);
            int shownCount = Math.Min(issues.Count, limit);
            List<string> lines = [.. issues.Take(shownCount).Select(Describe)];

            if (total > shownCount)
            {
                lines.Add(Say(Resource.ProjectPlan.Messages.Message_ImportMoreProblems, total - shownCount));
            }

            return lines;
        }

        /// <summary>
        /// The sentence that opens a message: the import is cancelled, and how many problems there are.
        /// </summary>
        public static string BuildHeader(int total) =>
            Say(Resource.ProjectPlan.Messages.Message_ImportProblemsFound, total);

        /// <summary>
        /// The whole message of an exception, as a host that prints only the message should say it.
        /// </summary>
        public static string BuildMessage(
            IReadOnlyList<ImportIssue> issues,
            int total,
            int limit = SummaryLimit)
        {
            ArgumentNullException.ThrowIfNull(issues);

            return $@"{BuildHeader(total)}{NewLineHelper.NewLine}{BuildLines(issues, total, limit)}";
        }

        /// <summary>
        /// The sentence that says how many of the problems are listed, for a host that lists only the first.
        /// </summary>
        public static string BuildListed(int listed) =>
            Say(Resource.ProjectPlan.Messages.Message_ImportFirstProblemsListed, listed);

        #endregion

        #region The report

        /// <summary>
        /// Writes the problems as CSV, one row for each, so that they can be sorted and filtered in a spreadsheet. A line ends
        /// with a line feed, a field with a comma, a quote or a line end is quoted, as RFC 4180 says, and a field that a
        /// spreadsheet would read as a formula starts with an apostrophe.
        /// </summary>
        public static void WriteReport(
            TextWriter writer,
            IEnumerable<ImportIssue> issues)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(issues);

            WriteRow(writer, @"Sheet", @"Cell", @"Row", @"Column", @"Column title", @"Row name", @"Value", @"Code", @"Message");

            foreach (ImportIssue issue in issues)
            {
                WriteRow(
                    writer,
                    Safe(issue.Sheet),
                    issue.Cell ?? string.Empty,
                    issue.Row?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    issue.Column?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    Safe(issue.ColumnTitle),
                    Safe(issue.RowName),
                    Safe(issue.Value),
                    issue.Code,
                    Safe(issue.Message));
            }
        }

        private static string Safe(string? field) =>
            !string.IsNullOrEmpty(field) && s_FormulaCharacters.Contains(field[0])
                ? $@"'{field}"
                : field ?? string.Empty;

        private static void WriteRow(TextWriter writer, params string[] fields)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0)
                {
                    writer.Write(',');
                }

                writer.Write(Quote(fields[i]));
            }

            writer.Write(NewLineHelper.NewLine);
        }

        private static string Quote(string field) =>
            field.AsSpan().IndexOfAny(s_QuotedCharacters) >= 0
                ? $@"""{field.Replace(@"""", @"""""", StringComparison.Ordinal)}"""
                : field;

        #endregion
    }
}
