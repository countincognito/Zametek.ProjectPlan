namespace Zametek.Graphs.Avalonia
{
    // The largest raster image that an export makes: the bitmap of the High Fidelity mode, and each PNG and JPEG image (the
    // clipboard takes a PNG image). An export draws a picture that is too large at a smaller scale, so that its image fits.
    // A picture that fits keeps the scale that the export asked for.
    //
    // Without a limit, the image of a large graph needed more memory than one image can have: the arrow graph of a plan of
    // 985 activities needed 62 GB at the scale of 2. Avalonia counts the bytes of a bitmap in 32 bits. Above 2 GiB the count
    // wraps, the bitmap gets a buffer that is too small, and the process crashes when Avalonia clears it. Skia gives no
    // surface for an image that it cannot allocate, and its JPEG encoder crashes the process on a side longer than 65,500
    // pixels.
    internal static class RasterSizeLimit
    {
        // 8,192 x 8,192 pixels: 256 MiB at 4 bytes for each pixel.
        public const long MaxPixelCount = 8_192L * 8_192L;

        // The longest side that the JPEG encoder accepts (JPEG_MAX_DIMENSION of libjpeg-turbo).
        public const int MaxSideLength = 65_500;

        // The scale for a picture of width x height units: the scale asked for, if the image fits at it. If not, a smaller
        // scale at which the image fits, within one pixel on each side of the largest such scale. A side of the image is the
        // side of the picture times the scale, rounded up to a whole pixel. A size with no area keeps its scale, because
        // there is nothing to fit.
        public static double FitScale(double width, double height, double scale)
        {
            if (!(width > 0.0) || !(height > 0.0) || !(scale > 0.0) || Fits(width, height, scale))
            {
                return scale;
            }

            // A rounded side is less than one pixel longer than the exact side. Thus a scale s at which
            // (width * s + 1) * (height * s + 1) <= MaxPixelCount, and (longest side) * s + 1 <= MaxSideLength, also fits after
            // the rounding. The bound of the area is the positive root of a quadratic in s, in a form that keeps its precision
            // for a large picture.
            double count = MaxPixelCount - 1.0;
            double sum = width + height;
            double areaScale = 2.0 * count / (sum + Math.Sqrt((sum * sum) + (4.0 * width * height * count)));
            double sideScale = (MaxSideLength - 1.0) / Math.Max(width, height);
            return Math.Min(scale, Math.Min(areaScale, sideScale));
        }

        private static bool Fits(double width, double height, double scale)
        {
            double pixelWidth = Math.Ceiling(width * scale);
            double pixelHeight = Math.Ceiling(height * scale);
            return pixelWidth * pixelHeight <= MaxPixelCount
                && Math.Max(pixelWidth, pixelHeight) <= MaxSideLength;
        }
    }
}
