using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectFileExport
    {
        void ExportProjectPlanFile(ProjectPlanModel projectPlan, ResourceSeriesSetModel resourceSeriesSet, TrackingSeriesSetModel trackingSeriesSet, bool showDates, string filename);

        Task ExportProjectPlanFileAsync(ProjectPlanModel projectPlan, ResourceSeriesSetModel resourceSeriesSet, TrackingSeriesSetModel trackingSeriesSet, bool showDates, string filename);
    }
}
