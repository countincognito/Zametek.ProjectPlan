using SkiaSharp;
using Svg.Skia;
using Zametek.Utility;

namespace Zametek.Graphs.ProjectPlan
{
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
                    .Case($".{Resource.ProjectPlan.Filters.Filter_ImageJpegFileExtension}", _ =>
                    {
                        using var stream = File.OpenWrite(filename);
                        picture.ToImage(
                            stream, SKColors.White, SKEncodedImageFormat.Jpeg, quality: 100, scaleX: scaleX, scaleY: scaleY,
                            skColorType: SKColorType.Argb4444, skAlphaType: SKAlphaType.Premul, skColorSpace: SKColorSpace.CreateSrgb());
                    })
                    .Case($".{Resource.ProjectPlan.Filters.Filter_ImagePngFileExtension}", _ =>
                    {
                        using var stream = File.OpenWrite(filename);
                        picture.ToImage(
                            stream, SKColors.White, SKEncodedImageFormat.Png, quality: 100, scaleX: scaleX, scaleY: scaleY,
                            skColorType: SKColorType.Argb4444, skAlphaType: SKAlphaType.Premul, skColorSpace: SKColorSpace.CreateSrgb());
                    })
                    .Case($".{Resource.ProjectPlan.Filters.Filter_PdfFileExtension}", _ =>
                    {
                        picture.ToPdf(filename, SKColors.White, scaleX: 2, scaleY: 2);
                    })
                    .Case($".{Resource.ProjectPlan.Filters.Filter_ImageSvgFileExtension}", _ =>
                    {
                        picture.ToSvg(filename, SKColors.White, scaleX: 2, scaleY: 2);
                    })
                    .Default(_ => throw new ArgumentOutOfRangeException(nameof(filename), @$"{Resource.ProjectPlan.Messages.Message_UnableToSaveFile} {filename}"));
            });
        }

        #endregion
    }
}
