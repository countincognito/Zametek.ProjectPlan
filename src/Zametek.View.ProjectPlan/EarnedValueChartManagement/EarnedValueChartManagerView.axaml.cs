using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ScottPlot;
using ScottPlot.Avalonia;
using System.Linq;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class EarnedValueChartManagerView
        : UserControl
    {
        public EarnedValueChartManagerView()
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

            // Milestones.

            AnnotatedVerticalLine? annotatedVerticalLine = plotModel.Plot.GetPlottables<AnnotatedVerticalLine>()
                .FirstOrDefault(arrow => arrow.CoordinateRect.Contains(mouseLocation));

            if (annotatedVerticalLine is not null)
            {
                scottplot.SetValue(ToolTip.TipProperty, annotatedVerticalLine.Annotation);
                return;
            }

            AnnotatedArrow? annotatedArrow = plotModel.Plot.GetPlottables<AnnotatedArrow>()
                .FirstOrDefault(rect => rect.CoordinateRect.Contains(mouseLocation));

            if (annotatedArrow is not null)
            {
                scottplot.SetValue(ToolTip.TipProperty, annotatedArrow.Annotation);
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

        private void ClearToolTip()
        {
            scottplot.ClearValue(ToolTip.TipProperty);
        }
    }
}
