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
        private const double c_DragThreshold = 3;
        private Point? m_DragStartPoint;
        private bool m_IsDragging;

        public EarnedValueChartManagerView()
        {
            InitializeComponent();

            m_DragStartPoint = null;
            m_IsDragging = false;

            scottplot.AddHandler(PointerPressedEvent, Scottplot_PointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            scottplot.AddHandler(PointerReleasedEvent, Scottplot_PointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);

            scottplot.Loaded += Scottplot_Loaded;
            scottplot.PointerExited += Scottplot_PointerExited;
            scottplot.PointerMoved += Scottplot_PointerMoved;
        }

        private void Scottplot_PointerPressed(
            object? sender,
            PointerPressedEventArgs e)
        {
            // Check if the pointer action is a click or a drag.
            PointerPointProperties properties = e.GetCurrentPoint(this).Properties;

            // Ensure it is the right mouse button
            if (properties.IsRightButtonPressed)
            {
                m_DragStartPoint = e.GetPosition(this);
                m_IsDragging = false;
            }
        }

        private void Scottplot_PointerReleased(
            object? sender,
            PointerReleasedEventArgs e)
        {
            if (m_DragStartPoint.HasValue
                && !m_IsDragging)
            {
                // Not dragging, so treat as a standard right-click.
                OpenScottPlotContextMenu(e);


            }
            // Reset the dragging state.
            m_DragStartPoint = null;
            m_IsDragging = false;

            e.Handled = true; ;
        }
        private void CheckPointerDrag(PointerEventArgs e)
        {
            if (m_DragStartPoint.HasValue)
            {
                Point currentPosition = e.GetPosition(this);
                double distance = Vector.Distance(m_DragStartPoint.Value, currentPosition);

                // Standard threshold to distinguish click vs. drag
                if (distance > c_DragThreshold
                    && !m_IsDragging)
                {
                    m_IsDragging = true;
                    // TODO: Call DragDrop.DoDragDropAsync here if starting a formal DND operation
                }
            }
        }

        private void OpenScottPlotContextMenu(PointerReleasedEventArgs e)
        {
            // Manually open the ContextMenu assigned to this container
            if (scottplot.ContextMenu != null)
            {
                scottplot.ContextMenu.Open(scottplot);

                // Mark as handled to prevent ScottPlot from doing anything else
                e.Handled = true;
            }
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
            CheckPointerDrag(e);

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
