using Shouldly;
using SkiaSharp;
using Xunit;

namespace Zametek.Graphs.Avalonia.Tests.Export
{
    // A raster image of a graph (PNG, JPEG and the clipboard) has at most 67,108,864 pixels (8,192 x 8,192), and no side is
    // longer than 65,500 pixels, the longest side that the JPEG encoder accepts. The exporter draws a larger picture at a
    // smaller scale, so that its image fits, and the image keeps the shape of the picture. A picture that fits keeps its
    // scale. Before the limit, a picture too large to allocate, or too wide for JPEG, gave an empty stream and no error.
    public class ImageSizeLimitTests
    {
        private const long c_MaxPixelCount = 8_192L * 8_192L;
        private const int c_MaxSideLength = 65_500;

        [Theory]
        [InlineData(GraphFileFormat.Png, 40f, 30f)]
        [InlineData(GraphFileFormat.Jpeg, 40f, 30f)]
        [InlineData(GraphFileFormat.Png, 4_096f, 4_096f)]
        public async Task A_picture_that_fits_keeps_the_scale_of_2(GraphFileFormat format, float width, float height)
        {
            // 4,096 x 4,096 units make exactly 8,192 x 8,192 pixels at the scale of 2: the limit itself.
            SKImageInfo image = await WriteAsync(format, width, height);

            image.Width.ShouldBe((int)(width * 2f));
            image.Height.ShouldBe((int)(height * 2f));
        }

        [Theory]
        [InlineData(GraphFileFormat.Png, 6_000f, 6_000f)]
        [InlineData(GraphFileFormat.Jpeg, 6_000f, 6_000f)]
        [InlineData(GraphFileFormat.Png, 100_000f, 20_000f)]
        [InlineData(GraphFileFormat.Jpeg, 40_000f, 100f)]
        public async Task A_picture_over_the_limit_is_drawn_at_a_scale_that_fits(GraphFileFormat format, float width, float height)
        {
            // At the scale of 2: 12,000 x 12,000 pixels; 200,000 x 40,000 pixels, too much memory for one image; and
            // 80,000 x 200 pixels, too wide for JPEG.
            SKImageInfo image = await WriteAsync(format, width, height);

            long pixelCount = (long)image.Width * image.Height;
            int longestSide = Math.Max(image.Width, image.Height);
            pixelCount.ShouldBeLessThanOrEqualTo(c_MaxPixelCount);
            longestSide.ShouldBeLessThanOrEqualTo(c_MaxSideLength);

            // As large as the limit lets it be, and the same shape as the picture.
            (pixelCount > 0.99 * c_MaxPixelCount || longestSide > 0.99 * c_MaxSideLength)
                .ShouldBeTrue($@"an image of {image.Width} x {image.Height} pixels uses most of the limit");
            ((double)image.Width / image.Height).ShouldBe(width / height, width / height * 0.01);
        }

        // Writes the picture at the default scale of 2, and reads the size of the image back.
        private static async Task<SKImageInfo> WriteAsync(GraphFileFormat format, float width, float height)
        {
            using SKPicture picture = BuildPicture(width, height);
            using var stream = new MemoryStream();

            await ImageExporter.WriteImageAsync(picture, stream, format);

            stream.Length.ShouldBeGreaterThan(0L, @"the exporter writes an image");
            SKImageInfo image = SKBitmap.DecodeBounds(stream.ToArray());
            image.Width.ShouldBeGreaterThan(0, @"the image decodes");
            return image;
        }

        private static SKPicture BuildPicture(float width, float height)
        {
            using var recorder = new SKPictureRecorder();
            SKCanvas canvas = recorder.BeginRecording(new SKRect(0f, 0f, width, height));
            using var paint = new SKPaint { Color = SKColors.SteelBlue };
            canvas.DrawRect(width * 0.1f, height * 0.1f, width * 0.8f, height * 0.8f, paint);
            return recorder.EndRecording();
        }
    }
}
