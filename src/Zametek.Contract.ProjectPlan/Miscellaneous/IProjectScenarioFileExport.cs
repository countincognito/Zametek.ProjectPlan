using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectScenarioFileExport
    {
        void ExportProjectScenarioFile(ProjectScenarioModel projectScenario, ResourceSeriesSetModel resourceSeriesSet, TrackingSeriesSetModel trackingSeriesSet, bool showDates, string filename);

        Task ExportProjectScenarioFileAsync(ProjectScenarioModel projectScenario, ResourceSeriesSetModel resourceSeriesSet, TrackingSeriesSetModel trackingSeriesSet, bool showDates, string filename);

        void ExportProjectScenarioXlsxFile(ProjectScenarioModel projectScenario, ResourceSeriesSetModel resourceSeriesSet, TrackingSeriesSetModel trackingSeriesSet, bool showDates, string filename);
    }
}
