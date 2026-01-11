using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectPlanFileImport
    {
        ProjectPlanImportModel ImportProjectPlanFile(string filename);

        Task<ProjectPlanImportModel> ImportProjectPlanFileAsync(string filename);

        ProjectPlanImportModel ImportMicrosoftProjectFile(string filename);

        ProjectPlanImportModel ImportProjectPlanXlsxFile(string filename);
    }
}
