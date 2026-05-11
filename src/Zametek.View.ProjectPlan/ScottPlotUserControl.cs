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

        // Left-button drag state (for subclasses).
        private Point? m_LeftDragStartPoint;
        private bool m_IsLeftDragging;

        // Ctrl+left-button drag state (for subclasses).
        private Point? m_CtrlLeftDragStartPoint;
        private bool m_IsCtrlLeftDragging;
        private bool m_CtrlWasHeldOnPress;

        protected ContentControl? m_PlotContainer;

        public ScottPlotUserControl()
        {
            m_DragStartPoint = null;
            m_IsDragging = false;
            m_LeftDragStartPoint = null;
            m_IsLeftDragging = false;
        }

        public void InitializePlotContainer(ContentControl plotContainer)
        {
            m_PlotContainer = plotContainer;
            m_DragStartPoint = null;
            m_IsDragging = false;
            m_LeftDragStartPoint = null;
            m_IsLeftDragging = false;
            m_CtrlLeftDragStartPoint = null;
            m_IsCtrlLeftDragging = false;
            m_CtrlWasHeldOnPress = false;

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

            if (properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
            {
                bool ctrlHeld = e.KeyModifiers.HasFlag(KeyModifiers.Control);
                m_CtrlWasHeldOnPress = ctrlHeld;

                if (ctrlHeld)
                {
                    m_CtrlLeftDragStartPoint = e.GetPosition(this);
                    m_IsCtrlLeftDragging = false;

                    if (m_PlotContainer?.Content is AvaPlot plotModelCtrl)
                    {
                        Point posCtrl = e.GetPosition(plotModelCtrl);
                        OnCtrlLeftDragStart(plotModelCtrl, posCtrl, e);
                    }
                }
                else
                {
                    m_LeftDragStartPoint = e.GetPosition(this);
                    m_IsLeftDragging = false;

                    if (m_PlotContainer?.Content is AvaPlot plotModel)
                    {
                        Point pos = e.GetPosition(plotModel);
                        OnLeftPointerPressed(plotModel, pos, e);
                    }
                }
            }

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

            if (properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
            {
                if (m_CtrlWasHeldOnPress)
                {
                    if (m_PlotContainer?.Content is AvaPlot plotModelCtrl)
                    {
                        Point posCtrl = e.GetPosition(plotModelCtrl);
                        OnCtrlLeftDragCompleted(plotModelCtrl, posCtrl, e);
                    }

                    m_CtrlLeftDragStartPoint = null;
                    m_IsCtrlLeftDragging = false;
                    m_CtrlWasHeldOnPress = false;
                }
                else
                {
                    if (m_PlotContainer?.Content is AvaPlot plotModel)
                    {
                        Point pos = e.GetPosition(plotModel);
                        if (m_IsLeftDragging)
                        {
                            OnLeftDragCompleted(plotModel, pos, e);
                        }
                        else
                        {
                            OnLeftPointerReleased(plotModel, pos, e);
                        }
                    }

                    m_LeftDragStartPoint = null;
                    m_IsLeftDragging = false;
                }
            }

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
            if (m_CtrlLeftDragStartPoint.HasValue)
            {
                Point currentPosition = e.GetPosition(this);
                double distance = Vector.Distance(m_CtrlLeftDragStartPoint.Value, currentPosition);

                if (distance > c_DragThreshold && !m_IsCtrlLeftDragging)
                {
                    m_IsCtrlLeftDragging = true;
                }
            }

            if (m_LeftDragStartPoint.HasValue)
            {
                Point currentPosition = e.GetPosition(this);
                double distance = Vector.Distance(m_LeftDragStartPoint.Value, currentPosition);

                if (distance > c_DragThreshold && !m_IsLeftDragging)
                {
                    m_IsLeftDragging = true;
                }
            }

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

        /// <summary>
        /// Called when the left mouse button is pressed on the plot. Override to implement custom behaviour.
        /// </summary>
        protected virtual void OnLeftPointerPressed(AvaPlot plotModel, Point plotPosition, PointerPressedEventArgs e) { }

        /// <summary>
        /// Called when the left mouse button is released after a drag. Override to commit the drag action.
        /// </summary>
        protected virtual void OnLeftDragCompleted(AvaPlot plotModel, Point plotPosition, PointerReleasedEventArgs e) { }

        /// <summary>
        /// Called when the left mouse button is released without a drag (i.e. a simple click). Override to cancel any pending state.
        /// </summary>
        protected virtual void OnLeftPointerReleased(AvaPlot plotModel, Point plotPosition, PointerReleasedEventArgs e) { }

        /// <summary>
        /// Called on every PointerMoved event while a left drag is in progress. Override for live preview.
        /// </summary>
        protected virtual void OnLeftDragging(AvaPlot plotModel, Point plotPosition, PointerEventArgs e) { }

        /// <summary>
        /// True while a left-button drag is active.
        /// </summary>
        protected bool IsLeftDragging => m_IsLeftDragging;

        /// <summary>
        /// Called when Ctrl+left mouse button is pressed on the plot. Override to begin a dependency drag.
        /// </summary>
        protected virtual void OnCtrlLeftDragStart(AvaPlot plotModel, Point plotPosition, PointerPressedEventArgs e) { }

        /// <summary>
        /// Called on every PointerMoved event while a Ctrl+left drag is in progress.
        /// </summary>
        protected virtual void OnCtrlLeftDragging(AvaPlot plotModel, Point plotPosition, PointerEventArgs e) { }

        /// <summary>
        /// Called when Ctrl+left mouse button is released after a drag. Override to commit the dependency.
        /// </summary>
        protected virtual void OnCtrlLeftDragCompleted(AvaPlot plotModel, Point plotPosition, PointerReleasedEventArgs e) { }

        /// <summary>
        /// True while a Ctrl+left-button drag is active.
        /// </summary>
        protected bool IsCtrlLeftDragging => m_IsCtrlLeftDragging;

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

            // Notify subclass of active Ctrl+left drag.
            if (m_IsCtrlLeftDragging)
            {
                OnCtrlLeftDragging(plotModel, pos, e);
                return;
            }

            // Notify subclass of active left drag.
            if (m_IsLeftDragging)
            {
                OnLeftDragging(plotModel, pos, e);
                return;
            }

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
