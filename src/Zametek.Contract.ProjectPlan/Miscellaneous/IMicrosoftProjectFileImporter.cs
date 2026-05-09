using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IMicrosoftProjectFileImporter
    {
        ProjectScenarioImportModel ImportMicrosoftProjectFile(string filename);
    }
}
