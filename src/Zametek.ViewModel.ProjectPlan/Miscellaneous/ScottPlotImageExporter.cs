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

        // Every chart draws in the same two bundled typefaces, and on Windows a typeface cannot be shared
        // while an SVG is written: Skia turns each run of glyphs back into characters for the SVG's text, and
        // under DirectWrite that races with any other thread drawing in the same typeface, leaving the run
        // with no text at all. Measured, a quarter to a half of SVG charts written alongside others lost
        // labels, and one SVG written while other charts drew as pictures lost them too; Linux's FreeType
        // typefaces lost none, and neither did PDFs. So an SVG is written while no other chart is being
        // drawn, and the pictures, which never map glyphs back to characters, still draw side by side.
        private static readonly ReaderWriterLockSlim s_TypefaceUse = new();

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

        private static void SharingTypefaces(Action draw)
        {
            s_TypefaceUse.EnterReadLock();
            try
            {
                draw();
            }
            finally
            {
                s_TypefaceUse.ExitReadLock();
            }
        }

        private static T SharingTypefaces<T>(Func<T> draw)
        {
            s_TypefaceUse.EnterReadLock();
            try
            {
                return draw();
            }
            finally
            {
                s_TypefaceUse.ExitReadLock();
            }
        }

        private static void AloneWithTypefaces(Action draw)
        {
            s_TypefaceUse.EnterWriteLock();
            try
            {
                draw();
            }
            finally
            {
                s_TypefaceUse.ExitWriteLock();
            }
        }

        #endregion

        #region IScottPlotImageExporter Members

        public async Task WritePlotImageAsync(Plot plot, Stream stream, ChartImageFormat format, int width, int height)
        {
            ArgumentNullException.ThrowIfNull(plot);
            ArgumentNullException.ThrowIfNull(stream);

            await Task.Run(() =>
            {
                if (format == ChartImageFormat.Svg)
                {
                    AloneWithTypefaces(() => WriteSvg(plot, stream, width, height));
                    return;
                }

                SharingTypefaces(() =>
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
                        case ChartImageFormat.Pdf:
                            WritePdf(plot, stream, width, height);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(format), format, null);
                    }
                });
            });
        }

        public Task<byte[]> RenderPlotImageAsync(Plot plot, int width, int height)
        {
            ArgumentNullException.ThrowIfNull(plot);
            return Task.Run(() => SharingTypefaces(
                () => plot.GetImageBytes(Math.Max(1, width), Math.Max(1, height), ImageFormat.Png)));
        }

        #endregion
    }
}
