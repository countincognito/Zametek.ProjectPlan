using ScottPlot;
using SkiaSharp;
using System.Text;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ScottPlotImageExporter
        : IScottPlotImageExporter
    {
        #region Fields

        // The quality the raster formats are encoded at (it only affects JPEG and WebP).
        private const int c_RasterQuality = 100;

        // The encoding ScottPlot saves an SVG file in: UTF-8, with no byte order mark.
        private static readonly UTF8Encoding s_SvgEncoding = new(encoderShouldEmitUTF8Identifier: false);

        #endregion

        #region Private Members

        private static void WritePdf(Plot plot, Stream stream, int width, int height)
        {
            using SKDocument pdfDocument = SKDocument.CreatePdf(stream, new SKDocumentPdfMetadata { RasterDpi = 300 });
            using (SKCanvas pdfCanvas = pdfDocument.BeginPage(width, height))
            {
                plot.Render(pdfCanvas, width, height);
                pdfDocument.EndPage();
            }
            pdfDocument.Close();
        }

        private static void WriteSvg(Plot plot, Stream stream, int width, int height)
        {
            stream.Write(s_SvgEncoding.GetBytes(plot.GetSvgXml(width, height)));
        }

        private static void WriteRaster(Plot plot, Stream stream, ImageFormat imageFormat, int width, int height)
        {
            using Image image = plot.GetImage(width, height);
            stream.Write(image.GetImageBytes(imageFormat, c_RasterQuality));
        }

        #endregion

        #region IScottPlotImageExporter Members

        public async Task WritePlotImageAsync(Plot plot, Stream stream, ChartImageFormat format, int width, int height)
        {
            ArgumentNullException.ThrowIfNull(plot);
            ArgumentNullException.ThrowIfNull(stream);

            await Task.Run(() =>
            {
                switch (format)
                {
                    case ChartImageFormat.Jpeg:
                        WriteRaster(plot, stream, ImageFormat.Jpeg, width, height);
                        break;
                    case ChartImageFormat.Png:
                        WriteRaster(plot, stream, ImageFormat.Png, width, height);
                        break;
                    case ChartImageFormat.Bmp:
                        WriteRaster(plot, stream, ImageFormat.Bmp, width, height);
                        break;
                    case ChartImageFormat.Webp:
                        WriteRaster(plot, stream, ImageFormat.Webp, width, height);
                        break;
                    case ChartImageFormat.Svg:
                        WriteSvg(plot, stream, width, height);
                        break;
                    case ChartImageFormat.Pdf:
                        WritePdf(plot, stream, width, height);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(format), format, null);
                }
            });
        }

        public Task<byte[]> RenderPlotImageAsync(Plot plot, int width, int height)
        {
            ArgumentNullException.ThrowIfNull(plot);
            return Task.Run(() => plot.GetImageBytes(Math.Max(1, width), Math.Max(1, height), ImageFormat.Png));
        }

        #endregion
    }
}
