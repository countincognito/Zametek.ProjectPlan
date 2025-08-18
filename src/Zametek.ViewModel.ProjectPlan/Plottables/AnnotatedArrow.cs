using ScottPlot;
using ScottPlot.Plottables;

namespace Zametek.ViewModel.ProjectPlan
{
    public class AnnotatedArrow
        : Arrow
    {
        public string Annotation { get; set; } = string.Empty;

        public CoordinateRect CoordinateRect
        {
            get
            {
                double left = Math.Min(Base.X, Tip.X);
                double right = Math.Max(Base.X, Tip.X);
                double bottom = Math.Min(Base.Y, Tip.Y);
                double top = Math.Max(Base.Y, Tip.Y);

                PixelRect pixelRect = Axes.DataRect;
                float leftPixel = Axes.XAxis.GetPixel(left, pixelRect) - ArrowheadWidth;
                float rightPixel = Axes.XAxis.GetPixel(right, pixelRect) + ArrowheadWidth;

                left = Axes.XAxis.GetCoordinate(leftPixel, pixelRect);
                right = Axes.XAxis.GetCoordinate(rightPixel, pixelRect);

                return new CoordinateRect(left, right, bottom, top);
            }
        }
    }
}
