using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaCursor = Avalonia.Input.Cursor;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;
using System.Linq;
using Zametek.Contract.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class GanttChartManagerView
        : ScottPlotUserControl
    {
        // Within this many pixels of the right edge of a bar counts as a resize handle.
        private const double c_RightEdgeHitPixels = 8.0;

        // State for the active bar resize drag.
        private AnnotatedBar? m_DragBar;
        private VerticalLine? m_DragPreviewLine;

        // State for Ctrl+drag dependency creation.
        private int m_DepSourceActivityId;
        private AnnotatedBar? m_DepSourceBar;

        public GanttChartManagerView()
        {
            InitializeComponent();
            InitializePlotContainer(scottplot);
        }

        /// <summary>
        /// Returns the AvaPlot embedded in the scottplot ContentControl, or null.
        /// </summary>
        private AvaPlot? GetPlotModel() =>
            scottplot.Content as AvaPlot;

        /// <summary>
        /// Hit-test: is the pixel position within <see cref="c_RightEdgeHitPixels"/> of the right edge
        /// of any annotated activity bar?
        /// </summary>
        private AnnotatedBar? HitTestRightEdge(AvaPlot plotModel, Point plotPosition)
        {
            BarPlot? barPlot = plotModel.Plot.GetPlottables<BarPlot>().FirstOrDefault();
            if (barPlot is null)
            {
                return null;
            }

            Pixel mousePixel = new(plotPosition.X, plotPosition.Y);

            foreach (Bar bar in barPlot.Bars)
            {
                if (bar is not AnnotatedBar annotatedBar || annotatedBar.ActivityId == 0)
                {
                    continue;
                }

                // Get the pixel coordinate of the bar's right edge (bar.Value is the right end in data coords).
                Pixel rightEdgePixel = plotModel.Plot.GetPixel(new Coordinates(annotatedBar.Value, annotatedBar.Position));

                double dx = mousePixel.X - rightEdgePixel.X;
                double dy = mousePixel.Y - rightEdgePixel.Y;
                double distX = System.Math.Abs(dx);

                // Only trigger if within the pixel threshold horizontally, and within the bar height vertically.
                // Bar height in pixels: Size is in data units on the Y axis, convert to pixels.
                Pixel topPixel = plotModel.Plot.GetPixel(
                    new Coordinates(annotatedBar.Value, annotatedBar.Position + annotatedBar.Size / 2.0));
                Pixel bottomPixel = plotModel.Plot.GetPixel(
                    new Coordinates(annotatedBar.Value, annotatedBar.Position - annotatedBar.Size / 2.0));

                double barPixelHeight = System.Math.Abs(topPixel.Y - bottomPixel.Y);
                double distY = System.Math.Abs(dy);

                if (distX <= c_RightEdgeHitPixels && distY <= barPixelHeight / 2.0)
                {
                    return annotatedBar;
                }
            }

            return null;
        }

        protected override bool OnLeftPointerPressed(AvaPlot plotModel, Point plotPosition, PointerPressedEventArgs e)
        {
            AnnotatedBar? hit = HitTestRightEdge(plotModel, plotPosition);
            if (hit is not null)
            {
                m_DragBar = hit;
                // Preview line is deferred to first OnLeftDragging call so a plain click leaves no artifact.
                // Capture to the ContentControl so our PointerMoved handler keeps firing for the full drag,
                // even if the pointer leaves the AvaPlot bounds. ScottPlot never enters pan mode because
                // PointerPressed is intercepted via Tunnel routing in ScottPlotUserControl before it reaches
                // AvaPlot — so there is no "sticky pan" state to worry about on release.
                e.Pointer.Capture(m_PlotContainer);
            }

            // Always claim left-click so ScottPlot never enters its own pan mode.
            e.Handled = true;
            return true;
        }

        protected override void OnLeftDragging(AvaPlot plotModel, Point plotPosition, PointerEventArgs e)
        {
            if (m_DragBar is null)
            {
                return;
            }

            // Add the preview line on the first dragging call (not on press, so clicks are clean).
            if (m_DragPreviewLine is null)
            {
                m_DragPreviewLine = plotModel.Plot.Add.VerticalLine(
                    m_DragBar.Value,
                    width: 2,
                    pattern: ScottPlot.LinePattern.Dashed);
            }

            Pixel mousePixel = new(plotPosition.X, plotPosition.Y);
            Coordinates mouseCoord = plotModel.Plot.GetCoordinates(mousePixel);

            // Compute how many days the new right edge represents.
            int newDuration = ComputeNewDuration(mouseCoord.X, m_DragBar);
            if (newDuration < 1)
            {
                newDuration = 1;
            }

            // Move preview line to the snapped position.
            double snappedX = m_DragBar.ValueBase + (mouseCoord.X - m_DragBar.ValueBase);
            // Snap to nearest integer day by re-deriving x from newDuration.
            double snappedDataX = DurationToDataX(m_DragBar, newDuration, m_DragBar.ValueBase);

            m_DragPreviewLine.X = snappedDataX;
            plotModel.Refresh();

            e.Handled = true;
        }

        protected override void OnLeftPointerReleased(AvaPlot plotModel, Point plotPosition, PointerReleasedEventArgs e)
        {
            // Click without drag: discard any pending bar state.
            if (m_DragPreviewLine is not null)
            {
                plotModel.Plot.Remove(m_DragPreviewLine);
                m_DragPreviewLine = null;
                plotModel.Refresh();
            }

            if (m_DragBar is not null)
            {
                // Capture was set in OnLeftPointerPressed — must release it here,
                // otherwise subsequent clicks (e.g. double-click) are routed incorrectly.
                e.Pointer.Capture(null);
            }

            m_DragBar = null;
        }

        protected override void OnLeftDragCompleted(AvaPlot plotModel, Point plotPosition, PointerReleasedEventArgs e)
        {
            if (m_DragBar is null)
            {
                return;
            }

            try
            {
                Pixel mousePixel = new(plotPosition.X, plotPosition.Y);
                Coordinates mouseCoord = plotModel.Plot.GetCoordinates(mousePixel);

                int newDuration = ComputeNewDuration(mouseCoord.X, m_DragBar);
                if (newDuration < 1)
                {
                    newDuration = 1;
                }

                int activityId = m_DragBar.ActivityId;

                // Commit to ViewModel.
                if (DataContext is IGanttChartManagerViewModel vm)
                {
                    vm.SetActivityDuration(activityId, newDuration);
                }
            }
            finally
            {
                // Remove preview line.
                if (m_DragPreviewLine is not null)
                {
                    plotModel.Plot.Remove(m_DragPreviewLine);
                    m_DragPreviewLine = null;
                }

                m_DragBar = null;
                plotModel.Refresh();

                e.Pointer.Capture(null);
                e.Handled = true;
            }
        }

        protected override AvaloniaCursor? GetHoverCursor(AvaPlot plotModel, Point plotPosition)
        {
            // Resize handle: near the right edge of any bar.
            if (HitTestRightEdge(plotModel, plotPosition) is not null)
            {
                return new AvaloniaCursor(StandardCursorType.SizeWestEast);
            }

            // Hand cursor: hovering over the body of any bar (Ctrl+drag to create dependency).
            if (HitTestBar(plotModel, plotPosition) is not null)
            {
                return new AvaloniaCursor(StandardCursorType.Hand);
            }

            return AvaloniaCursor.Default;
        }

        /// <summary>
        /// Hit-test: is the pixel position inside any annotated activity bar?
        /// </summary>
        private AnnotatedBar? HitTestBar(AvaPlot plotModel, Point plotPosition)
        {
            BarPlot? barPlot = plotModel.Plot.GetPlottables<BarPlot>().FirstOrDefault();
            if (barPlot is null)
            {
                return null;
            }

            Pixel mousePixel = new(plotPosition.X, plotPosition.Y);
            Coordinates mouseCoord = plotModel.Plot.GetCoordinates(mousePixel);

            foreach (Bar bar in barPlot.Bars)
            {
                if (bar is not AnnotatedBar annotatedBar || annotatedBar.ActivityId == 0)
                {
                    continue;
                }

                if (bar.Rect.Contains(mouseCoord))
                {
                    return annotatedBar;
                }
            }

            return null;
        }

        protected override bool OnCtrlLeftDragStart(AvaPlot plotModel, Point plotPosition, PointerPressedEventArgs e)
        {
            AnnotatedBar? hit = HitTestBar(plotModel, plotPosition);
            if (hit is null)
            {
                return false;
            }

            m_DepSourceActivityId = hit.ActivityId;
            m_DepSourceBar = hit;
            e.Pointer.Capture(m_PlotContainer);
            e.Handled = true;
            return true;
        }

        protected override void OnCtrlLeftDragging(AvaPlot plotModel, Point plotPosition, PointerEventArgs e)
        {
            // No live preview for simplicity — cursor change is sufficient feedback.
            e.Handled = true;
        }

        protected override void OnCtrlLeftDragCompleted(AvaPlot plotModel, Point plotPosition, PointerReleasedEventArgs e)
        {
            try
            {
                if (m_DepSourceActivityId == 0)
                {
                    return;
                }

                AnnotatedBar? targetBar = HitTestBar(plotModel, plotPosition);
                if (targetBar is null || targetBar.ActivityId == m_DepSourceActivityId)
                {
                    return;
                }

                if (DataContext is IGanttChartManagerViewModel vm)
                {
                    vm.AddActivityDependency(m_DepSourceActivityId, targetBar.ActivityId);
                }
            }
            finally
            {
                m_DepSourceActivityId = 0;
                m_DepSourceBar = null;
                e.Pointer.Capture(null);
                e.Handled = true;
            }
        }

        /// <summary>
        /// Converts a new right-edge X data coordinate into an integer duration (days),
        /// using the bar's original data-units-per-day ratio.
        /// </summary>
        private static int ComputeNewDuration(double newRightX, AnnotatedBar bar)
        {
            if (bar.ActivityDuration <= 0)
            {
                return 1;
            }

            double originalDataWidth = bar.Value - bar.ValueBase;
            if (System.Math.Abs(originalDataWidth) < double.Epsilon)
            {
                return 1;
            }

            double dataPerDay = originalDataWidth / bar.ActivityDuration;
            double newDataWidth = newRightX - bar.ValueBase;
            int newDuration = (int)System.Math.Round(newDataWidth / dataPerDay);

            return newDuration;
        }

        /// <summary>
        /// Given a duration (days), compute what the right-edge X data coordinate should be.
        /// </summary>
        private static double DurationToDataX(AnnotatedBar bar, int duration, double leftX)
        {
            if (bar.ActivityDuration <= 0)
            {
                return leftX + duration;
            }

            double originalDataWidth = bar.Value - bar.ValueBase;
            double dataPerDay = originalDataWidth / bar.ActivityDuration;
            return leftX + (duration * dataPerDay);
        }
    }
}
