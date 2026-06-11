using java.util.stream;
using ScottPlot;
using SkiaSharp;
using sun.swing;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ScottPlotImageExporter
        : IScottPlotImageExporter
    {
        #region IScottPlotImageExporter Members

        public async Task SavePlotImageAsync(Plot plot, string filename, int width, int height)
        {
            ArgumentNullException.ThrowIfNull(plot);
            ArgumentException.ThrowIfNullOrWhiteSpace(filename);

            await Task.Run(() =>
            {
                string fileExtension = Path.GetExtension(filename);
                bool matched = false;
                bool isPdf = false;

                fileExtension.ValueSwitchOn()
                    .Case($".{Resource.ProjectPlan.Filters.Filter_ImageJpegFileExtension}", _ => matched = true)
                    .Case($".{Resource.ProjectPlan.Filters.Filter_ImagePngFileExtension}", _ => matched = true)
                    .Case($".{Resource.ProjectPlan.Filters.Filter_ImageBmpFileExtension}", _ => matched = true)
                    .Case($".{Resource.ProjectPlan.Filters.Filter_ImageWebpFileExtension}", _ => matched = true)
                    .Case($".{Resource.ProjectPlan.Filters.Filter_ImageSvgFileExtension}", _ => matched = true)
                    .Case($".{Resource.ProjectPlan.Filters.Filter_PdfFileExtension}", _ => { matched = true; isPdf = true; })
                    .Default(_ => throw new ArgumentOutOfRangeException(nameof(filename), @$"{Resource.ProjectPlan.Messages.Message_UnableToSaveFile} {filename}"));

                if (matched)
                {
                    if (isPdf)
                    {
                        using FileStream stream = File.OpenWrite(filename);
                        using SKDocument pdfDocument = SKDocument.CreatePdf(stream, new SKDocumentPdfMetadata { RasterDpi = 300 });
                        using (SKCanvas pdfCanvas = pdfDocument.BeginPage(width, height))
                        {
                            plot.Render(pdfCanvas, width, height);
                            pdfDocument.EndPage();
                        }
                        pdfDocument.Close();
                    }
                    else
                    {
                        plot.Save(filename, width, height, ImageFormats.FromFilename(filename), 100);
                    }
                }
            });
        }

        #endregion
    }
}
