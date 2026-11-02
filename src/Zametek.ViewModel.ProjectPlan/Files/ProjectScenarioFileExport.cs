using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

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
            Stream stream,
            ProjectScenarioExportFormat format)
        {
            ArgumentNullException.ThrowIfNull(stream);

            switch (format)
            {
                case ProjectScenarioExportFormat.Xlsx:
                    m_XlsxScenarioExporter.ExportProjectScenarioXlsxFile(projectScenario, resourceSeriesSet, trackingSeriesSet, showDates, stream);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }

        #endregion
    }
}
