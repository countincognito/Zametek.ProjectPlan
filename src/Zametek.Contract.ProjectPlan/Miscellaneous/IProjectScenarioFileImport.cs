using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectScenarioFileImport
    {
        ProjectScenarioImportModel ImportProjectScenarioFile(string filename);

        Task<ProjectScenarioImportModel> ImportProjectScenarioFileAsync(string filename);

        ProjectScenarioImportModel ImportMicrosoftProjectFile(string filename);

        ProjectScenarioImportModel ImportProjectScenarioXlsxFile(string filename);
    }
}
