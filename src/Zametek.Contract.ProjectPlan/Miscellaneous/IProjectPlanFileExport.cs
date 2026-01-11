using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectPlanFileExport
    {
        void ExportProjectPlanFile(ProjectPlanModel projectPlan, ResourceSeriesSetModel resourceSeriesSet, TrackingSeriesSetModel trackingSeriesSet, bool showDates, string filename);

        Task ExportProjectPlanFileAsync(ProjectPlanModel projectPlan, ResourceSeriesSetModel resourceSeriesSet, TrackingSeriesSetModel trackingSeriesSet, bool showDates, string filename);

        void ExportProjectPlanXlsxFile(ProjectPlanModel projectPlan, ResourceSeriesSetModel resourceSeriesSet, TrackingSeriesSetModel trackingSeriesSet, bool showDates, string filename);
    }
}
