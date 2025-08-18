using ScottPlot;
using ScottPlot.Plottables;

namespace Zametek.ViewModel.ProjectPlan
{
    public class AnnotatedVerticalLine
        : VerticalLine
    {
        public string Annotation { get; set; } = string.Empty;

        public CoordinateRect CoordinateRect
        {
            get
            {
                PixelRect pixelRect = Axes.DataRect;

                float leftPixel = Axes.XAxis.GetPixel(X, pixelRect) - LineWidth;
                float rightPixel = Axes.XAxis.GetPixel(X, pixelRect) + LineWidth;

                double left = Axes.XAxis.GetCoordinate(leftPixel, pixelRect);
                double right = Axes.XAxis.GetCoordinate(rightPixel, pixelRect);

                double bottom = Minimum;
                double top = Maximum;
                return new CoordinateRect(left, right, bottom, top);
            }
        }
    }
}
