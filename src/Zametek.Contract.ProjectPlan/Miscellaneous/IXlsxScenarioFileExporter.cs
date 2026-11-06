using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IXlsxScenarioFileExporter
    {
        // Writes a workbook to the stream. The caller owns the stream, which is left open.
        void ExportProjectScenarioXlsxFile(
            ProjectScenarioModel projectScenario,
            ResourceSeriesSetModel resourceSeriesSet,
            TrackingSeriesSetModel trackingSeriesSet,
            bool showDates,
            Stream stream);
    }
}
