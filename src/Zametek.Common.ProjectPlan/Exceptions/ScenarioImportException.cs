namespace Zametek.Common.ProjectPlan
{
    /// <summary>
    /// Thrown when a file that is imported has problems. Nothing is imported from such a file: the importer reads the whole
    /// file, collects every problem that it finds, and then stops. The message is a short summary (the number of problems and
    /// the first few), so that a host which prints only the message still says what is wrong; the whole list is in
    /// <see cref="Issues"/>.
    /// </summary>
    public class ScenarioImportException
        : Exception
    {
        public ScenarioImportException()
        {
            Issues = [];
        }

        public ScenarioImportException(string message)
            : base(message)
        {
            Issues = [];
        }

        public ScenarioImportException(string message, Exception innerException)
            : base(message, innerException)
        {
            Issues = [];
        }

        public ScenarioImportException(
            string message,
            IReadOnlyList<ImportIssue> issues,
            int totalCount)
            : base(message)
        {
            ArgumentNullException.ThrowIfNull(issues);
            Issues = issues;
            TotalCount = Math.Max(totalCount, issues.Count);
        }

        /// <summary>
        /// The problems, in the order in which they were found. A file can have more of them than the importer keeps (see
        /// <see cref="TotalCount"/>).
        /// </summary>
        public IReadOnlyList<ImportIssue> Issues { get; }

        /// <summary>
        /// The number of problems that were found. It is more than the number of <see cref="Issues"/> when the importer
        /// stopped keeping them.
        /// </summary>
        public int TotalCount { get; }
    }
}
