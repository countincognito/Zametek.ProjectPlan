using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectScenarioFileExport
    {
        // Writes the project scenario to the stream in the given format. The caller owns the stream, which is left open.
        void ExportProjectScenarioFile(ProjectScenarioModel projectScenario, ResourceSeriesSetModel resourceSeriesSet, TrackingSeriesSetModel trackingSeriesSet, bool showDates, Stream stream, ProjectScenarioExportFormat format);
    }
}
