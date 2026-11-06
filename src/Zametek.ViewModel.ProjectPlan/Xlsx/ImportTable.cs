using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// A sheet of a workbook as the importer reads it: the titles of the columns, and the rows under them, each with the number
    /// that it has in the sheet. The titles are the structure of the sheet. A column is found by its title and not by its place,
    /// so a column that is missing, or one that is added, does not move the others, and a title that the importer does not know
    /// is left alone.
    /// </summary>
    /// <remarks>
    /// The table holds no part of the library that read the file, and thus a change of the library changes only the code that
    /// makes the tables (see <see cref="XlsxWorkbookReader"/>).
    /// </remarks>
    internal sealed class ImportTable
    {
        private readonly Dictionary<string, int> m_ColumnIndexes = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> m_UnusableTitles = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<ImportRow> m_Rows = [];

        private ImportTable(
            string sheetName,
            IReadOnlyList<string> titles,
            ImportIssueCollector issues)
        {
            SheetName = sheetName;
            Titles = titles;
            Issues = issues;
        }

        public string SheetName { get; }

        /// <summary>
        /// The titles, up to the first one that is blank.
        /// </summary>
        public IReadOnlyList<string> Titles { get; }

        /// <summary>
        /// The rows that hold at least one value in the columns that have a title.
        /// </summary>
        public IReadOnlyList<ImportRow> Rows => m_Rows;

        public ImportIssueCollector Issues { get; }

        /// <summary>
        /// Makes the table of a sheet. A title that the importer reads (<paramref name="knownTitles"/>) must be the title of one
        /// column: if two columns have it, the importer cannot know which one to read, and that is a problem.
        /// </summary>
        public static ImportTable Create(
            string sheetName,
            IReadOnlyList<ImportCell> header,
            IEnumerable<(int Number, ImportCell[] Cells)> rows,
            IReadOnlyList<string> knownTitles,
            ImportIssueCollector issues)
        {
            ArgumentNullException.ThrowIfNull(sheetName);
            ArgumentNullException.ThrowIfNull(header);
            ArgumentNullException.ThrowIfNull(rows);
            ArgumentNullException.ThrowIfNull(knownTitles);
            ArgumentNullException.ThrowIfNull(issues);

            // The text of a text cell is the title as it is, and not trimmed: a title with a space around it is not the title.
            string[] titles = [.. header.Select(x => x.Kind == ImportCellKind.Text ? x.Text : x.Display)];
            var table = new ImportTable(sheetName, titles, issues);

            for (int i = 0; i < titles.Length; i++)
            {
                table.m_ColumnIndexes.TryAdd(titles[i], i);
            }

            foreach (string known in knownTitles)
            {
                List<int> places = [.. Enumerable.Range(0, titles.Length).Where(i => string.Equals(titles[i], known, StringComparison.OrdinalIgnoreCase))];

                if (places.Count < 2)
                {
                    continue;
                }

                table.m_UnusableTitles.Add(known);

                foreach (int place in places.Skip(1))
                {
                    issues.Add(new ImportIssue
                    {
                        Code = ImportIssueCodes.DuplicateColumn,
                        Message = ImportIssueText.DuplicateColumn(titles[place], ImportIssue.ColumnLetters(places[0] + 1)),
                        Sheet = sheetName,
                        Row = 1,
                        Column = place + 1,
                        ColumnTitle = titles[place],
                        Value = titles[place],
                    });
                }
            }

            foreach ((int number, ImportCell[] cells) in rows)
            {
                table.m_Rows.Add(new ImportRow(table, number, cells));
            }

            return table;
        }

        /// <summary>
        /// The place of the column with this title (the first of the columns that have it, whatever the case of the letters), or
        /// null when there is no such column, or when two columns have the title and the problem was reported.
        /// </summary>
        public int? IndexOf(string title)
        {
            ArgumentNullException.ThrowIfNull(title);

            return !m_UnusableTitles.Contains(title) && m_ColumnIndexes.TryGetValue(title, out int index)
                ? index
                : null;
        }

        public bool HasColumn(string title) => IndexOf(title) is not null;

        /// <summary>
        /// Says that the sheet must have a column, if it has any rows. A sheet with the titles and nothing under them needs
        /// nothing from them. Returns whether the column can be read.
        /// </summary>
        public bool RequireColumn(string title)
        {
            ArgumentNullException.ThrowIfNull(title);

            if (m_UnusableTitles.Contains(title))
            {
                return false;
            }

            if (HasColumn(title))
            {
                return true;
            }

            if (m_Rows.Count > 0)
            {
                Issues.Add(new ImportIssue
                {
                    Code = ImportIssueCodes.MissingColumn,
                    Message = ImportIssueText.MissingColumn(title),
                    Sheet = SheetName,
                    Row = 1,
                    ColumnTitle = title,
                });
            }

            return false;
        }
    }
}
