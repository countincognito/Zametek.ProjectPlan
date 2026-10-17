using Zametek.Common.ProjectPlan;

namespace Zametek.Engine.ProjectPlan
{
    /// <summary>
    /// Thrown when a job cannot read the project or workbook it was given: the file is not one the engine opens - it is
    /// not a project, it is of a version the engine does not know, or its content is damaged. The message is the
    /// reader's own, so that a host which prints it prints what it always printed; the reader's exception is the inner
    /// one. It tells a file the engine cannot read from a failure of the engine itself, which a host that answers a
    /// caller needs to do.
    /// <para>
    /// A workbook that has problems in its cells is read in full, and the reader's exception is a
    /// <see cref="ScenarioImportException"/> with the list of them (see <see cref="ImportProblems"/>).
    /// </para>
    /// </summary>
    public class ProjectNotReadableException
        : Exception
    {
        public ProjectNotReadableException()
        {
        }

        public ProjectNotReadableException(string message)
            : base(message)
        {
        }

        public ProjectNotReadableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// The problems that were found in the cells of a workbook, or null when the file cannot be read for another reason.
        /// </summary>
        public ScenarioImportException? ImportProblems => InnerException as ScenarioImportException;
    }
}
