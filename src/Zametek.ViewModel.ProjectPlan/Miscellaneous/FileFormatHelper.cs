using Zametek.Common.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    // The format a file name's extension names, for the callers that start from a file name: the desktop's dialogs and
    // zpp's import and export options. The file layer itself takes the format alongside a stream, which has no name to
    // read one from. An extension outside the list is refused with the message the file layer gave when it was handed
    // file names.
    public static class FileFormatHelper
    {
        public static ProjectScenarioImportFormat GetProjectScenarioImportFormat(string filename)
        {
            ArgumentNullException.ThrowIfNull(filename);
            ProjectScenarioImportFormat? format = null;

            Path.GetExtension(filename).ValueSwitchOn()
                .Case($".{Resource.ProjectPlan.Filters.Filter_MicrosoftProjectMppFileExtension}", _ => format = ProjectScenarioImportFormat.MicrosoftProject)
                .Case($".{Resource.ProjectPlan.Filters.Filter_MicrosoftProjectXmlFileExtension}", _ => format = ProjectScenarioImportFormat.MicrosoftProject)
                .Case($".{Resource.ProjectPlan.Filters.Filter_ProjectXlsxFileExtension}", _ => format = ProjectScenarioImportFormat.Xlsx);

            return format ?? throw new ArgumentOutOfRangeException(
                nameof(filename),
                @$"{Resource.ProjectPlan.Messages.Message_UnableToImportFile} {filename}");
        }

        public static ProjectScenarioExportFormat GetProjectScenarioExportFormat(string filename)
        {
            ArgumentNullException.ThrowIfNull(filename);
            ProjectScenarioExportFormat? format = null;

            Path.GetExtension(filename).ValueSwitchOn()
                .Case($".{Resource.ProjectPlan.Filters.Filter_ProjectXlsxFileExtension}", _ => format = ProjectScenarioExportFormat.Xlsx);

            return format ?? throw new ArgumentOutOfRangeException(
                nameof(filename),
                @$"{Resource.ProjectPlan.Messages.Message_UnableToExportFile} {filename}");
        }

        public static ChartImageFormat GetChartImageFormat(string filename)
        {
            ArgumentNullException.ThrowIfNull(filename);
            ChartImageFormat? format = null;

            Path.GetExtension(filename).ValueSwitchOn()
                .Case($".{Resource.ProjectPlan.Filters.Filter_ImageJpegFileExtension}", _ => format = ChartImageFormat.Jpeg)
                .Case($".{Resource.ProjectPlan.Filters.Filter_ImagePngFileExtension}", _ => format = ChartImageFormat.Png)
                .Case($".{Resource.ProjectPlan.Filters.Filter_ImageBmpFileExtension}", _ => format = ChartImageFormat.Bmp)
                .Case($".{Resource.ProjectPlan.Filters.Filter_ImageWebpFileExtension}", _ => format = ChartImageFormat.Webp)
                .Case($".{Resource.ProjectPlan.Filters.Filter_ImageSvgFileExtension}", _ => format = ChartImageFormat.Svg)
                .Case($".{Resource.ProjectPlan.Filters.Filter_PdfFileExtension}", _ => format = ChartImageFormat.Pdf);

            return format ?? throw new ArgumentOutOfRangeException(
                nameof(filename),
                @$"{Resource.ProjectPlan.Messages.Message_UnableToSaveFile} {filename}");
        }
    }
}
