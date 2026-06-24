using SkiaSharp;
using Svg.Skia;
using Zametek.Utility;

namespace Zametek.Graphs.Avalonia
{
    // Writes a recorded graph picture (either the interactive canvas or a rasterised MSAGL SVG) to a
    // JPEG, PNG, PDF or SVG file. An internal SkiaSharp implementation detail of the reusable interactive
    // view-model's export path - it takes an SKPicture, so it is not part of the framework-neutral public
    // surface (the former public IGraphImageExporter seam was removed for that reason). The neutral
    // public export entry point is IInteractiveGraph.SaveImageAsync(filename, source).
    internal static class GraphImageExporter
    {
        public static async Task SaveGraphImageAsync(SKPicture picture, string filename, int scaleX = 2, int scaleY = 2)
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
                        picture.ToImage(
                            stream, SKColors.White, SKEncodedImageFormat.Jpeg, quality: 100, scaleX: scaleX, scaleY: scaleY,
                            skColorType: SKColorType.Argb4444, skAlphaType: SKAlphaType.Premul, skColorSpace: SKColorSpace.CreateSrgb());
                    })
                    .Case($".{GraphFileExtensions.Png}", _ =>
                    {
                        using var stream = File.OpenWrite(filename);
                        picture.ToImage(
                            stream, SKColors.White, SKEncodedImageFormat.Png, quality: 100, scaleX: scaleX, scaleY: scaleY,
                            skColorType: SKColorType.Argb4444, skAlphaType: SKAlphaType.Premul, skColorSpace: SKColorSpace.CreateSrgb());
                    })
                    .Case($".{GraphFileExtensions.Pdf}", _ =>
                    {
                        picture.ToPdf(filename, SKColors.White, scaleX: 2, scaleY: 2);
                    })
                    .Case($".{GraphFileExtensions.Svg}", _ =>
                    {
                        picture.ToSvg(filename, SKColors.White, scaleX: 2, scaleY: 2);
                    })
                    .Default(_ => throw new ArgumentOutOfRangeException(nameof(filename), @$"{Messages.Message_UnableToSaveFile} {filename}"));
            });
        }
    }
}
