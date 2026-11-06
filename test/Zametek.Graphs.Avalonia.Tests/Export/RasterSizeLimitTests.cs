using Shouldly;
using Xunit;

namespace Zametek.Graphs.Avalonia.Tests.Export
{
    // The scale of a raster image: the largest scale, up to the one asked for, at which the image fits the limit. Each side of
    // the image is the side of the picture times the scale, rounded up to a whole pixel.
    public class RasterSizeLimitTests
    {
        [Fact]
        public void The_limit_is_8192_by_8192_pixels_and_65500_pixels_on_a_side()
        {
            RasterSizeLimit.MaxPixelCount.ShouldBe(67_108_864L);
            RasterSizeLimit.MaxSideLength.ShouldBe(65_500);
        }

        [Theory]
        [InlineData(40.0, 30.0, 2.0)]
        [InlineData(14_802.4, 306.5, 2.0)]
        [InlineData(4_096.0, 4_096.0, 2.0)]
        [InlineData(8_192.0, 8_192.0, 1.0)]
        [InlineData(32_750.0, 10.0, 2.0)]
        public void A_size_that_fits_keeps_its_scale(double width, double height, double scale)
        {
            // The arrow graph of docs/assets/website-refresh.zpp; exactly the limit at the scale of 2, and at the scale of 1
            // (a bitmap of the High Fidelity mode, written again); and exactly the longest side.
            RasterSizeLimit.FitScale(width, height, scale).ShouldBe(scale);
        }

        [Theory]
        [InlineData(148_184.4, 26_111.4)]
        [InlineData(20_684.8, 48_966.4)]
        [InlineData(47_236.9, 8_922.9)]
        [InlineData(8_837.9, 14_838.9)]
        [InlineData(40_000.0, 100.0)]
        [InlineData(1e9, 1.0)]
        [InlineData(1.0, 1e9)]
        [InlineData(1e7, 1e7)]
        public void A_size_over_the_limit_gets_the_largest_scale_that_fits(double width, double height)
        {
            // The graphs of docs/assets/erp-migration.zpp and docs/assets/mobile-app.zpp, a picture too wide for JPEG only,
            // and extremes.
            double scale = RasterSizeLimit.FitScale(width, height, 2.0);

            scale.ShouldBeLessThan(2.0);
            (long pixelCount, long longestSide) = ShouldFit(width, height, scale);
            (pixelCount > 0.999 * RasterSizeLimit.MaxPixelCount || longestSide > 0.999 * RasterSizeLimit.MaxSideLength)
                .ShouldBeTrue($@"{pixelCount} pixels and a longest side of {longestSide} pixels use most of the limit");
        }

        [Fact]
        public void Each_scale_gives_an_image_that_fits_and_a_size_that_fits_keeps_its_scale()
        {
            double[] sides = [0.5, 1.0, 7.3, 100.0, 1_000.0, 8_191.5, 8_192.0, 32_750.0, 33_000.0, 65_500.0, 100_000.0, 1e6, 1e8];
            double[] scales = [0.25, 1.0, 2.0, 4.0];

            foreach (double width in sides)
            {
                foreach (double height in sides)
                {
                    foreach (double asked in scales)
                    {
                        double scale = RasterSizeLimit.FitScale(width, height, asked);

                        scale.ShouldBeLessThanOrEqualTo(asked);
                        ShouldFit(width, height, scale);
                        if (Fits(width, height, asked))
                        {
                            scale.ShouldBe(asked, $@"{width} x {height} at the scale of {asked} fits");
                        }
                    }
                }
            }
        }

        [Theory]
        [InlineData(0.0, 10.0)]
        [InlineData(10.0, 0.0)]
        [InlineData(-5.0, 10.0)]
        [InlineData(double.NaN, 10.0)]
        public void A_size_with_no_area_keeps_its_scale(double width, double height)
        {
            // There is nothing to fit. The caller deals with a picture of this size.
            RasterSizeLimit.FitScale(width, height, 2.0).ShouldBe(2.0);
        }

        private static bool Fits(double width, double height, double scale)
        {
            long pixelWidth = (long)Math.Ceiling(width * scale);
            long pixelHeight = (long)Math.Ceiling(height * scale);
            return pixelWidth * pixelHeight <= RasterSizeLimit.MaxPixelCount
                && Math.Max(pixelWidth, pixelHeight) <= RasterSizeLimit.MaxSideLength;
        }

        private static (long PixelCount, long LongestSide) ShouldFit(double width, double height, double scale)
        {
            long pixelWidth = (long)Math.Ceiling(width * scale);
            long pixelHeight = (long)Math.Ceiling(height * scale);
            string image = $@"{width} x {height} at the scale of {scale}: {pixelWidth} x {pixelHeight} pixels";
            (pixelWidth * pixelHeight).ShouldBeLessThanOrEqualTo(RasterSizeLimit.MaxPixelCount, image);
            Math.Max(pixelWidth, pixelHeight).ShouldBeLessThanOrEqualTo(RasterSizeLimit.MaxSideLength, image);
            return (pixelWidth * pixelHeight, Math.Max(pixelWidth, pixelHeight));
        }
    }
}
