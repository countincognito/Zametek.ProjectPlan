using SkiaSharp;
using Svg.Skia;
using Zametek.Utility;

namespace Zametek.Graphs.ProjectPlan
{
    // Writes a recorded graph picture (either the interactive canvas or a rasterised MSAGL SVG) to a
    // JPEG, PNG, PDF or SVG file. Moved into the control library so the reusable interactive graph
    // view-models can export images without depending on the application.
    public class GraphImageExporter
        : IGraphImageExporter
    {
        #region IGraphImageExporter Members

        public async Task SaveGraphImageAsync(SKPicture picture, string filename, int scaleX = 2, int scaleY = 2)
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

        #endregion
    }
}
