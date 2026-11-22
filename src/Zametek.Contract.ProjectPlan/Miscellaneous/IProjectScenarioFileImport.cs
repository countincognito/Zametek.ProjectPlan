using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectScenarioFileImport
    {
        // Reads a project scenario in the given format from the stream. The caller owns the stream, which is left open.
        // A cancellation stops the read of a workbook between two rows. The read of a file of Microsoft Project does not
        // stop.
        ProjectScenarioImportModel ImportProjectScenarioFile(Stream stream, ProjectScenarioImportFormat format, CancellationToken cancellationToken = default);
    }
}
