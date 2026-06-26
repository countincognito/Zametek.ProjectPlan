using SkiaSharp;
using Svg.Skia;
using Zametek.Utility;

namespace Zametek.Graphs.Avalonia
{
    // Writes or renders a recorded SKPicture (the interactive canvas, or a rasterised MSAGL SVG) as a
    // JPEG, PNG, PDF or SVG. Each format offers Write* (to a Stream) and RenderTo* (to a byte[]), each
    // with an async variant; SaveImageAsync picks the format from a file's extension. An internal
    // SkiaSharp implementation detail of the export / clipboard paths - it takes an SKPicture, so it is
    // not part of the framework-neutral public surface. The neutral public export entry point is
    // IInteractiveGraph.SaveImageAsync(filename, source, imageType).
    internal static class ImageExporter
    {
        #region Png

        public static void WritePng(SKPicture picture, Stream stream, int scaleX = 2, int scaleY = 2) =>
            WriteRaster(picture, stream, SKEncodedImageFormat.Png, scaleX, scaleY);

        public static Task WritePngAsync(SKPicture picture, Stream stream, int scaleX = 2, int scaleY = 2) =>
            Task.Run(() => WritePng(picture, stream, scaleX, scaleY));

        public static byte[] RenderToPng(SKPicture picture, int scaleX = 2, int scaleY = 2) =>
            RenderToBytes(picture, WritePng, scaleX, scaleY);

        public static Task<byte[]> RenderToPngAsync(SKPicture picture, int scaleX = 2, int scaleY = 2) =>
            Task.Run(() => RenderToPng(picture, scaleX, scaleY));

        #endregion

        #region Jpeg

        public static void WriteJpeg(SKPicture picture, Stream stream, int scaleX = 2, int scaleY = 2) =>
            WriteRaster(picture, stream, SKEncodedImageFormat.Jpeg, scaleX, scaleY);

        public static Task WriteJpegAsync(SKPicture picture, Stream stream, int scaleX = 2, int scaleY = 2) =>
            Task.Run(() => WriteJpeg(picture, stream, scaleX, scaleY));

        public static byte[] RenderToJpeg(SKPicture picture, int scaleX = 2, int scaleY = 2) =>
            RenderToBytes(picture, WriteJpeg, scaleX, scaleY);

        public static Task<byte[]> RenderToJpegAsync(SKPicture picture, int scaleX = 2, int scaleY = 2) =>
            Task.Run(() => RenderToJpeg(picture, scaleX, scaleY));

        #endregion

        #region Pdf

        public static void WritePdf(SKPicture picture, Stream stream, int scaleX = 2, int scaleY = 2)
        {
            ArgumentNullException.ThrowIfNull(picture);
            picture.ToPdf(stream, SKColors.White, scaleX, scaleY);
        }

        public static Task WritePdfAsync(SKPicture picture, Stream stream, int scaleX = 2, int scaleY = 2) =>
            Task.Run(() => WritePdf(picture, stream, scaleX, scaleY));

        public static byte[] RenderToPdf(SKPicture picture, int scaleX = 2, int scaleY = 2) =>
            RenderToBytes(picture, WritePdf, scaleX, scaleY);

        public static Task<byte[]> RenderToPdfAsync(SKPicture picture, int scaleX = 2, int scaleY = 2) =>
            Task.Run(() => RenderToPdf(picture, scaleX, scaleY));

        #endregion

        #region Svg

        public static void WriteSvg(SKPicture picture, Stream stream, int scaleX = 2, int scaleY = 2)
        {
            ArgumentNullException.ThrowIfNull(picture);
            picture.ToSvg(stream, SKColors.White, scaleX, scaleY);
        }

        public static Task WriteSvgAsync(SKPicture picture, Stream stream, int scaleX = 2, int scaleY = 2) =>
            Task.Run(() => WriteSvg(picture, stream, scaleX, scaleY));

        public static byte[] RenderToSvg(SKPicture picture, int scaleX = 2, int scaleY = 2) =>
            RenderToBytes(picture, WriteSvg, scaleX, scaleY);

        public static Task<byte[]> RenderToSvgAsync(SKPicture picture, int scaleX = 2, int scaleY = 2) =>
            Task.Run(() => RenderToSvg(picture, scaleX, scaleY));

        #endregion

        #region File save

        // Convenience entry point: pick the format from the file's extension and write it. Used by the
        // on-screen Save-As path.
        public static async Task SaveImageAsync(SKPicture picture, string filename, int scaleX = 2, int scaleY = 2)
        {
            ArgumentNullException.ThrowIfNull(picture);
            ArgumentException.ThrowIfNullOrWhiteSpace(filename);

            await Task.Run(() =>
            {
                string fileExtension = Path.GetExtension(filename);

                fileExtension.ValueSwitchOn()
                    .Case($".{GraphFileExtensions.Jpeg}", _ =>
                    {
                        using var stream = File.OpenWrite(filename);
                        WriteJpeg(picture, stream, scaleX, scaleY);
                    })
                    .Case($".{GraphFileExtensions.Png}", _ =>
                    {
                        using var stream = File.OpenWrite(filename);
                        WritePng(picture, stream, scaleX, scaleY);
                    })
                    .Case($".{GraphFileExtensions.Pdf}", _ =>
                    {
                        using var stream = File.OpenWrite(filename);
                        WritePdf(picture, stream, scaleX, scaleY);
                    })
                    .Case($".{GraphFileExtensions.Svg}", _ =>
                    {
                        using var stream = File.OpenWrite(filename);
                        WriteSvg(picture, stream, scaleX, scaleY);
                    })
                    .Default(_ => throw new ArgumentOutOfRangeException(nameof(filename), @$"{Messages.Message_UnableToSaveFile} {filename}"));
            });
        }

        #endregion

        #region Private Members

        // Raster (PNG/JPEG) share one path. Rgba8888 keeps full colour depth so the output stays crisp.
        private static void WriteRaster(SKPicture picture, Stream stream, SKEncodedImageFormat format, int scaleX, int scaleY)
        {
            ArgumentNullException.ThrowIfNull(picture);
            picture.ToImage(
                stream, SKColors.White, format, quality: 100, scaleX: scaleX, scaleY: scaleY,
                skColorType: SKColorType.Rgba8888, skAlphaType: SKAlphaType.Premul, skColorSpace: SKColorSpace.CreateSrgb());
        }

        private static byte[] RenderToBytes(SKPicture picture, Action<SKPicture, Stream, int, int> write, int scaleX, int scaleY)
        {
            ArgumentNullException.ThrowIfNull(picture);
            using var stream = new MemoryStream();
            write(picture, stream, scaleX, scaleY);
            return stream.ToArray();
        }

        #endregion
    }
}
