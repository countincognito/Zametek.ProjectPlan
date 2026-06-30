using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IXlsxScenarioFileImporter
    {
        ProjectScenarioImportModel ImportProjectScenarioXlsxFile(string filename);
    }
}
