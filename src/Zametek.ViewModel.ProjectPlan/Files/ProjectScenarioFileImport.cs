using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ProjectScenarioFileImport
        : IProjectScenarioFileImport
    {
        #region Fields

        private readonly IMicrosoftProjectFileImporter m_MicrosoftProjectFileImporter;
        private readonly IXlsxScenarioFileImporter m_XlsxFileImporter;

        #endregion

        #region Ctors

        public ProjectScenarioFileImport(
            IMicrosoftProjectFileImporter microsoftProjectFileImporter,
            IXlsxScenarioFileImporter xlsxFileImporter)
        {
            ArgumentNullException.ThrowIfNull(microsoftProjectFileImporter);
            ArgumentNullException.ThrowIfNull(xlsxFileImporter);
            m_MicrosoftProjectFileImporter = microsoftProjectFileImporter;
            m_XlsxFileImporter = xlsxFileImporter;
        }

        #endregion

        #region IProjectScenarioFileImport Members

        public ProjectScenarioImportModel ImportProjectScenarioFile(Stream stream, ProjectScenarioImportFormat format, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);

            return format switch
            {
                ProjectScenarioImportFormat.MicrosoftProject => m_MicrosoftProjectFileImporter.ImportMicrosoftProjectFile(stream),
                ProjectScenarioImportFormat.Xlsx => m_XlsxFileImporter.ImportProjectScenarioXlsxFile(stream, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
            };
        }

        #endregion
    }
}
