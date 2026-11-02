using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Keeps the problems that an import finds. The import reads the whole file, whatever it finds, and when it has read all
    /// of it <see cref="ThrowIfAny"/> stops it with the list.
    /// </summary>
    /// <remarks>
    /// A file can have a very large number of problems (a column of ten thousand cells that all say the wrong thing), and a
    /// list of that size is not a thing to keep, so the collector keeps the first <see cref="MaximumKept"/> and counts the rest.
    /// </remarks>
    internal sealed class ImportIssueCollector
    {
        public const int MaximumKept = 10_000;

        private readonly List<ImportIssue> m_Issues = [];

        /// <summary>
        /// The number of problems found, with those that were not kept.
        /// </summary>
        public int Count { get; private set; }

        public IReadOnlyList<ImportIssue> Issues => m_Issues;

        public void Add(ImportIssue issue)
        {
            ArgumentNullException.ThrowIfNull(issue);
            Count++;

            if (m_Issues.Count < MaximumKept)
            {
                m_Issues.Add(issue);
            }
        }

        /// <summary>
        /// Stops the import when there is a problem to report.
        /// </summary>
        public void ThrowIfAny()
        {
            if (Count > 0)
            {
                throw ToException();
            }
        }

        public ScenarioImportException ToException()
        {
            // The problems are in the order in which a person reads the workbook: sheet by sheet, in the order in which the
            // import came to them, and in each sheet row by row and column by column (the import does not read the cells of a
            // row in the order of the columns). A problem with a whole sheet comes before those of its cells.
            Dictionary<string, int> sheets = [];

            foreach (ImportIssue issue in m_Issues)
            {
                sheets.TryAdd(issue.Sheet ?? string.Empty, sheets.Count);
            }

            List<ImportIssue> ordered =
            [
                .. m_Issues
                    .OrderBy(x => sheets[x.Sheet ?? string.Empty])
                    .ThenBy(x => x.Row ?? 0)
                    .ThenBy(x => x.Column ?? 0),
            ];

            return new ScenarioImportException(ImportIssueText.BuildMessage(ordered, Count), ordered, Count);
        }
    }
}
