using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectFileImport
    {
        ProjectImportModel ImportProjectFile(string filename);

        Task<ProjectImportModel> ImportProjectFileAsync(string filename);

        ProjectImportModel ImportMicrosoftProjectMppFile(string filename);

        Task<ProjectImportModel> ImportMicrosoftProjectMppFileAsync(string filename);

        ProjectImportModel ImportMicrosoftProjectXmlFile(string filename);

        Task<ProjectImportModel> ImportMicrosoftProjectXmlFileAsync(string filename);

        ProjectImportModel ImportProjectXlsxFile(string filename);
    }
}
