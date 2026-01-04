using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectFileExport
    {
        void ExportProjectFile(ProjectModel project, ResourceSeriesSetModel resourceSeriesSet, TrackingSeriesSetModel trackingSeriesSet, bool showDates, string filename);

        Task ExportProjectFileAsync(ProjectModel project, ResourceSeriesSetModel resourceSeriesSet, TrackingSeriesSetModel trackingSeriesSet, bool showDates, string filename);

        void ExportProjectXlsxFile(ProjectModel project, ResourceSeriesSetModel resourceSeriesSet, TrackingSeriesSetModel trackingSeriesSet, bool showDates, string filename);
    }
}
