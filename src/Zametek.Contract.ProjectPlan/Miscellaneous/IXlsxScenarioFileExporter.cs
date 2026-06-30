using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IXlsxScenarioFileExporter
    {
        void ExportProjectScenarioXlsxFile(
            ProjectScenarioModel projectScenario,
            ResourceSeriesSetModel resourceSeriesSet,
            TrackingSeriesSetModel trackingSeriesSet,
            bool showDates,
            string filename);
    }
}
