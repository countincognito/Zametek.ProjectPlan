using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IXlsxFileImporter
    {
        ProjectScenarioImportModel ImportProjectScenarioXlsxFile(string filename);
    }
}
