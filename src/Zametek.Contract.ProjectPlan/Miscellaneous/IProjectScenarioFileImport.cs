using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectScenarioFileImport
    {
        // Reads a project scenario in the given format from the stream. The caller owns the stream, which is left open.
        ProjectScenarioImportModel ImportProjectScenarioFile(Stream stream, ProjectScenarioImportFormat format);
    }
}
