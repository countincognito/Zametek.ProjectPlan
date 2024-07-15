using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectFileExport
    {
        void ExportProjectFile(ProjectPlanModel projectPlan, ResourceSeriesSetModel resourceSeriesSet, TrackingSeriesSetModel trackingSeriesSet, bool showDates, string filename);

        Task ExportProjectFileAsync(ProjectPlanModel projectPlan, ResourceSeriesSetModel resourceSeriesSet, TrackingSeriesSetModel trackingSeriesSet, bool showDates, string filename);

        void ExportProjectXlsxFile(ProjectPlanModel projectPlan, ResourceSeriesSetModel resourceSeriesSet, TrackingSeriesSetModel trackingSeriesSet, bool showDates, string filename);
    }
}
