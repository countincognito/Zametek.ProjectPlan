using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ProjectScenarioFileExport
        : IProjectScenarioFileExport
    {
        #region Fields

        private readonly IXlsxScenarioFileExporter m_XlsxScenarioExporter;

        #endregion

        #region Ctors

        public ProjectScenarioFileExport(IXlsxScenarioFileExporter xlsxScenarioExporter)
        {
            ArgumentNullException.ThrowIfNull(xlsxScenarioExporter);
            m_XlsxScenarioExporter = xlsxScenarioExporter;
        }

        #endregion

        #region IProjectFileExport Members

        public void ExportProjectScenarioFile(
            ProjectScenarioModel projectScenario,
            ResourceSeriesSetModel resourceSeriesSet,
            TrackingSeriesSetModel trackingSeriesSet,
            bool showDates,
            string filename)
        {
            string fileExtension = Path.GetExtension(filename);

            Action<ProjectScenarioModel, ResourceSeriesSetModel, TrackingSeriesSetModel, bool, string> action =
                (projectScenario, resourceSeriesSet, trackingSeriesSet, showDates, filename) => throw new ArgumentOutOfRangeException(
                    nameof(filename),
                    @$"{Resource.ProjectPlan.Messages.Message_UnableToExportFile} {filename}");

            fileExtension.ValueSwitchOn()
                .Case($".{Resource.ProjectPlan.Filters.Filter_ProjectXlsxFileExtension}", _ => action = ExportProjectScenarioXlsxFile);

            action(projectScenario, resourceSeriesSet, trackingSeriesSet, showDates, filename);
        }

        public async Task ExportProjectScenarioFileAsync(
            ProjectScenarioModel projectScenario,
            ResourceSeriesSetModel resourceSeriesSet,
            TrackingSeriesSetModel trackingSeriesSet,
            bool showDates,
            string filename)
        {
            await Task.Run(() => ExportProjectScenarioFile(projectScenario, resourceSeriesSet, trackingSeriesSet, showDates, filename));
        }

        public void ExportProjectScenarioXlsxFile(
            ProjectScenarioModel projectScenario,
            ResourceSeriesSetModel resourceSeriesSet,
            TrackingSeriesSetModel trackingSeriesSet,
            bool showDates,
            string filename)
        {
            m_XlsxScenarioExporter.ExportProjectScenarioXlsxFile(projectScenario, resourceSeriesSet, trackingSeriesSet, showDates, filename);
        }

        #endregion
    }
}
