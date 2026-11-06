using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IMicrosoftProjectFileImporter
    {
        // Reads an MS Project plan (.mpp or .xml, told apart by content) from the stream. The caller owns the stream, which
        // is left open.
        ProjectScenarioImportModel ImportMicrosoftProjectFile(Stream stream);
    }
}
