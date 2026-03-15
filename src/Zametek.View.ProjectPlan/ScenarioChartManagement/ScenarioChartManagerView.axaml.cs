using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;
using System.Linq;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class ScenarioChartManagerView
        : UserControl
    {
        public ScenarioChartManagerView()
        {
            InitializeComponent();
            scottplot.Loaded += Scottplot_Loaded;
            scottplot.PointerExited += Scottplot_PointerExited;
            scottplot.PointerMoved += Scottplot_PointerMoved;
        }

        private void Scottplot_Loaded(object? sender, RoutedEventArgs e)
        {
            ClearToolTip();
        }

        private void Scottplot_PointerExited(object? sender, RoutedEventArgs e)
        {
            ClearToolTip();
        }

        private void Scottplot_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (scottplot.Content is not AvaPlot plotModel)
            {
                return;
            }

            Point pos = e.GetPosition(plotModel);
            Pixel mousePixel = new(pos.X, pos.Y);
            Coordinates mouseLocation = plotModel.Plot.GetCoordinates(mousePixel);

            // Scenario points.

            AnnotatedMarker? annotatedMarker = plotModel.Plot.GetPlottables<AnnotatedMarker>()
                .FirstOrDefault(marker => IsPointInMarker(plotModel.Plot, mouseLocation, marker));

            if (annotatedMarker is not null)
            {
                scottplot.SetValue(ToolTip.TipProperty, annotatedMarker.Annotation);
                return;
            }

            // Annotations.

            AnnotatedRectangle? annotatedRectangle = plotModel.Plot.GetPlottables<AnnotatedRectangle>()
                .FirstOrDefault(rect => rect.CoordinateRect.Contains(mouseLocation));

            if (annotatedRectangle is not null)
            {
                scottplot.SetValue(ToolTip.TipProperty, annotatedRectangle.Annotation);
                return;
            }

            // If no bar or rectangle was found, clear the tooltip.
            ClearToolTip();
        }

        private static bool IsPointInMarker(
            Plot plot,
            Coordinates point,
            Marker marker)
        {

            // Get pixel location of the marker center.
            Pixel markerPixel = plot.GetPixel(marker.Coordinates);

            // Get pixel location of the point center.
            Pixel pointPixel = plot.GetPixel(point);

            // Calculate pixel rectangle based on MarkerSize (e.g. 10).
            float halfSize = marker.MarkerStyle.Size / 2;
            ScottPlot.PixelRect markerRect = new(
                left: markerPixel.X - halfSize,
                right: markerPixel.X + halfSize,
                bottom: markerPixel.Y + halfSize,
                top: markerPixel.Y - halfSize
            );

            // Check if the point pixel is within the marker rectangle.
            return markerRect.Contains(pointPixel);
        }

        private void ClearToolTip()
        {
            scottplot.ClearValue(ToolTip.TipProperty);
        }
    }
}
