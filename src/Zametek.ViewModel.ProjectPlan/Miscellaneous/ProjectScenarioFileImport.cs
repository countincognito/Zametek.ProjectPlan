using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ProjectScenarioFileImport
        : IProjectScenarioFileImport
    {
        #region Fields

        private readonly IMicrosoftProjectFileImporter m_MicrosoftProjectFileImporter;
        private readonly IXlsxFileImporter m_XlsxFileImporter;

        #endregion

        #region Ctors

        public ProjectScenarioFileImport(
            IMicrosoftProjectFileImporter microsoftProjectFileImporter,
            IXlsxFileImporter xlsxFileImporter)
        {
            ArgumentNullException.ThrowIfNull(microsoftProjectFileImporter);
            ArgumentNullException.ThrowIfNull(xlsxFileImporter);
            m_MicrosoftProjectFileImporter = microsoftProjectFileImporter;
            m_XlsxFileImporter = xlsxFileImporter;
        }

        #endregion

        #region IProjectScenarioFileImport Members

        public ProjectScenarioImportModel ImportProjectScenarioFile(string filename)
        {
            string fileExtension = Path.GetExtension(filename);

            Func<string, ProjectScenarioImportModel> func =
                filename => throw new ArgumentOutOfRangeException(
                    nameof(filename),
                    @$"{Resource.ProjectPlan.Messages.Message_UnableToImportFile} {filename}");

            fileExtension.ValueSwitchOn()
                .Case($".{Resource.ProjectPlan.Filters.Filter_MicrosoftProjectMppFileExtension}", _ => func = ImportMicrosoftProjectFile)
                .Case($".{Resource.ProjectPlan.Filters.Filter_MicrosoftProjectXmlFileExtension}", _ => func = ImportMicrosoftProjectFile)
                .Case($".{Resource.ProjectPlan.Filters.Filter_ProjectXlsxFileExtension}", _ => func = ImportProjectScenarioXlsxFile);

            return func(filename);
        }

        public async Task<ProjectScenarioImportModel> ImportProjectScenarioFileAsync(string filename)
        {
            return await Task.Run(() => ImportProjectScenarioFile(filename));
        }

        public ProjectScenarioImportModel ImportMicrosoftProjectFile(string filename)
        {
            return m_MicrosoftProjectFileImporter.ImportMicrosoftProjectFile(filename);
        }

        public ProjectScenarioImportModel ImportProjectScenarioXlsxFile(string filename)
        {
            return m_XlsxFileImporter.ImportProjectScenarioXlsxFile(filename);
        }

        #endregion
    }
}
