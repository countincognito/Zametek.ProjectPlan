using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IXlsxScenarioFileImporter
    {
        // Reads a workbook from the stream. The caller owns the stream, which is left open. A cancellation stops the read
        // between two rows.
        ProjectScenarioImportModel ImportProjectScenarioXlsxFile(Stream stream, CancellationToken cancellationToken = default);
    }
}
