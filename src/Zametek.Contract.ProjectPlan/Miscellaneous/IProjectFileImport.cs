using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectFileImport
    {
        ProjectImportModel ImportProjectFile(string filename);

        Task<ProjectImportModel> ImportProjectFileAsync(string filename);

        ProjectImportModel ImportMicrosoftProjectFile(string filename);

        ProjectImportModel ImportProjectXlsxFile(string filename);
    }
}
