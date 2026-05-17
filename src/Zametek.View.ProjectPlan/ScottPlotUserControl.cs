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
    public class ScottPlotUserControl
        : UserControl
    {
        private const double c_DragThreshold = 5;
        private Point? m_DragStartPoint;
        private bool m_IsDragging;

        protected ContentControl? m_PlotContainer;

        public ScottPlotUserControl()
        {
            m_DragStartPoint = null;
            m_IsDragging = false;
        }

        public void InitializePlotContainer(ContentControl plotContainer)
        {
            m_PlotContainer = plotContainer;
            m_DragStartPoint = null;
            m_IsDragging = false;

            m_PlotContainer.AddHandler(PointerPressedEvent, PlotContainer_PointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
            m_PlotContainer.AddHandler(PointerReleasedEvent, PlotContainer_PointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);

            m_PlotContainer.Loaded += PlotContainer_Loaded;
            m_PlotContainer.PointerExited += PlotContainer_PointerExited;
            m_PlotContainer.PointerMoved += PlotContainer_PointerMoved;
        }

        private void PlotContainer_PointerPressed(
            object? sender,
            PointerPressedEventArgs e)
        {
            // Check if the pointer action is a click or a drag.
            PointerPointProperties properties = e.GetCurrentPoint(this).Properties;

            // Ensure it is the right mouse button
            if (properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed)
            {
                m_DragStartPoint = e.GetPosition(this);
                m_IsDragging = false;
                e.Handled = true;
            }
        }

        private void PlotContainer_PointerReleased(
            object? sender,
            PointerReleasedEventArgs e)
        {
            // Check if the pointer action is a click or a drag.
            PointerPointProperties properties = e.GetCurrentPoint(this).Properties;

            // Ensure it is the right mouse button
            if (properties.PointerUpdateKind == PointerUpdateKind.RightButtonReleased
                && !m_IsDragging)
            {
                // Not dragging, so treat as a standard right-click.
                OpenPlotContainerContextMenu(e);
            }

            // Reset the dragging state.
            m_DragStartPoint = null;
            m_IsDragging = false;
            e.Handled = true;
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

        private void OpenPlotContainerContextMenu(PointerReleasedEventArgs e)
        {
            // Manually open the ContextMenu assigned to this container
            if (m_PlotContainer?.ContextMenu != null)
            {
                m_PlotContainer.ContextMenu.Open(m_PlotContainer);

                // Mark as handled to prevent ScottPlot from doing anything else
                e.Handled = true;
            }
        }

        private void PlotContainer_Loaded(object? sender, RoutedEventArgs e)
        {
            ClearToolTip();
        }

        private void PlotContainer_PointerExited(object? sender, RoutedEventArgs e)
        {
            ClearToolTip();
        }

        private void PlotContainer_PointerMoved(object? sender, PointerEventArgs e)
        {
            CheckPointerDrag(e);

            if (m_PlotContainer?.Content is not AvaPlot plotModel)
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
                m_PlotContainer.SetValue(ToolTip.TipProperty, annotatedVerticalLine.Annotation);
                return;
            }

            AnnotatedArrow? annotatedArrow = plotModel.Plot.GetPlottables<AnnotatedArrow>()
                .FirstOrDefault(rect => rect.CoordinateRect.Contains(mouseLocation));

            if (annotatedArrow is not null)
            {
                m_PlotContainer.SetValue(ToolTip.TipProperty, annotatedArrow.Annotation);
                return;
            }

            // Activity bars.

            if (plotModel.Plot.GetPlottables<BarPlot>().FirstOrDefault() is BarPlot barPlot)
            {
                Bar? bar = barPlot.Bars.FirstOrDefault(bar => bar.Rect.Contains(mouseLocation));

                if (bar is AnnotatedBar annotatedBar)
                {
                    m_PlotContainer.SetValue(ToolTip.TipProperty, annotatedBar.Annotation);
                    return;
                }
            }

            // Scenario points.

            AnnotatedMarker? annotatedMarker = plotModel.Plot.GetPlottables<AnnotatedMarker>()
                .FirstOrDefault(marker => IsPointInMarker(plotModel.Plot, mouseLocation, marker));

            if (annotatedMarker is not null)
            {
                m_PlotContainer.SetValue(ToolTip.TipProperty, annotatedMarker.Annotation);
                return;
            }

            // Annotations.

            AnnotatedRectangle? annotatedRectangle = plotModel.Plot.GetPlottables<AnnotatedRectangle>()
                .FirstOrDefault(rect => rect.CoordinateRect.Contains(mouseLocation));

            if (annotatedRectangle is not null)
            {
                m_PlotContainer.SetValue(ToolTip.TipProperty, annotatedRectangle.Annotation);
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
            m_PlotContainer?.ClearValue(ToolTip.TipProperty);
        }
    }
}
