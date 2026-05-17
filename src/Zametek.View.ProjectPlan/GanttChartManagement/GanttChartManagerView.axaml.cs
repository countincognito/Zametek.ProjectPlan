using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;
using System;
using System.Linq;
using System.Text;
using Zametek.Contract.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;
using AvaloniaInput = Avalonia.Input;

namespace Zametek.View.ProjectPlan
{
    public partial class GanttChartManagerView
        : ScottPlotUserControl
    {
        private const double c_ResizeEdgePixelTolerance = 8.0;

        private readonly IDateTimeCalculator m_DateTimeCalculator;

        private bool m_IsResizeDragging;
        private int? m_ResizeActivityId;
        private int? m_ResizeActivityDuration;
        private int? m_ResizeActivityStartTime;
        private AnnotatedBar? m_ResizingBar;

        private static readonly AvaloniaInput.Cursor s_SizeWestEastCursor = new(StandardCursorType.SizeWestEast);
        private static readonly AvaloniaInput.Cursor s_HandCursor = new(StandardCursorType.Hand);

        public GanttChartManagerView(IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            InitializeComponent();
            InitializePlotContainer(scottplot);
            m_DateTimeCalculator = dateTimeCalculator;
            m_IsResizeDragging = false;
            m_ResizeActivityId = null;
            m_ResizeActivityDuration = null;
            m_ResizeActivityStartTime = null;
            m_ResizingBar = null;

            scottplot.AddHandler(PointerPressedEvent, Gantt_PointerPressed, RoutingStrategies.Tunnel, handledEventsToo: false);
            scottplot.PointerMoved += Gantt_PointerMoved;
            scottplot.AddHandler(PointerReleasedEvent, Gantt_PointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        }

        private void Gantt_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            PointerPointProperties props = e.GetCurrentPoint(this).Properties;

            if (!props.IsLeftButtonPressed
                || !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                return;
            }

            if (m_PlotContainer?.Content is not AvaPlot plot)
            {
                return;
            }

            Avalonia.Point pos = e.GetPosition(plot);
            Pixel mousePixel = new(pos.X, pos.Y);

            AnnotatedBar? hit = HitTestBarRightEdge(plot, mousePixel);

            if (hit is null)
            {
                return;
            }

            m_IsResizeDragging = true;
            m_ResizeActivityId = hit.ActivityId;
            m_ResizeActivityDuration = hit.Duration;
            m_ResizeActivityStartTime = hit.StartTime;
            m_ResizingBar = hit;

            // Suppress ScottPlot's default left-drag pan.
            e.Handled = true;
        }

        private void Gantt_PointerMoved(
            object? sender,
            PointerEventArgs e)
        {
            if (m_PlotContainer?.Content is not AvaPlot plot)
            {
                return;
            }

            Avalonia.Point pos = e.GetPosition(plot);
            Pixel mousePixel = new(pos.X, pos.Y);

            if (m_IsResizeDragging
                && m_ResizingBar is not null)
            {
                Coordinates coords = plot.Plot.GetCoordinates(mousePixel);
                double newRightEdge = Math.Max(m_ResizingBar.ValueBase + 1, coords.X);
                m_ResizingBar.Value = newRightEdge;
                plot.Refresh();

                if (DataContext is IGanttChartManagerViewModel vm
                    && m_ResizeActivityStartTime is not null)
                {
                    int newDuration = CalculateNewDuration(newRightEdge, m_ResizeActivityStartTime.GetValueOrDefault(), vm);

                    Canvas.SetLeft(dragTooltipBorder, pos.X + 16);
                    Canvas.SetTop(dragTooltipBorder, pos.Y + 4);

                    var tooltipText = new StringBuilder($@"{Resource.ProjectPlan.Labels.Label_Duration}: {newDuration}");

                    if (m_ResizeActivityDuration is not null)
                    {
                        int diff = newDuration - m_ResizeActivityDuration.GetValueOrDefault();
                        if (diff != 0)
                        {
                            tooltipText.Append($@" ({diff:+#;-#;0})");
                        }
                    }

                    dragTooltipText.Text = tooltipText.ToString();
                    dragTooltipBorder.IsVisible = true;
                }
            }

            AnnotatedBar? hit = HitTestBarRightEdge(plot, mousePixel);
            AvaloniaInput.Cursor hoverCursor = s_HandCursor;

            // Cursor hint when hovering near a bar's right edge while Shift is held.
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                hoverCursor = s_SizeWestEastCursor;
            }

            Cursor = hit is not null
                ? hoverCursor
                : AvaloniaInput.Cursor.Default;
        }

        private void Gantt_PointerReleased(
            object? sender,
            PointerReleasedEventArgs e)
        {
            dragTooltipBorder.IsVisible = false;

            if (!m_IsResizeDragging)
            {
                return;
            }

            if (m_PlotContainer?.Content is AvaPlot plot
                && m_ResizeActivityId is not null
                && m_ResizeActivityDuration is not null
                && m_ResizeActivityStartTime is not null
                && DataContext is IGanttChartManagerViewModel vm)
            {
                Avalonia.Point pos = e.GetPosition(plot);
                Pixel mousePixel = new(pos.X, pos.Y);
                Coordinates coords = plot.Plot.GetCoordinates(mousePixel);

                double finalX = m_ResizingBar is not null
                    ? Math.Max(m_ResizingBar.ValueBase + 1, coords.X)
                    : coords.X;

                int startTimeUnit = m_ResizeActivityStartTime.GetValueOrDefault();
                int newDuration = CalculateNewDuration(finalX, startTimeUnit, vm);
                vm.SetActivityDuration(m_ResizeActivityId.GetValueOrDefault(), newDuration);
            }

            m_IsResizeDragging = false;
            m_ResizeActivityId = null;
            m_ResizeActivityDuration = null;
            m_ResizeActivityStartTime = null;
            m_ResizingBar = null;
            Cursor = AvaloniaInput.Cursor.Default;
        }

        private int CalculateNewDuration(
            double finalX,
            int startTimeUnit,
            IGanttChartManagerViewModel vm)
        {
            int? finishTimeUnit;

            if (vm.ShowDates)
            {
                // ShowDates=true: X is an OLE Automation date. Convert back to a time unit.
                DateTime newFinishDate = DateTime.FromOADate(finalX);
                (finishTimeUnit, _) = m_DateTimeCalculator
                    .CalculateTimeAndDateTime(
                        vm.ProjectStart,
                        new DateTimeOffset(newFinishDate));
            }
            else
            {
                int newFinishDate = (int)Math.Round(finalX);
                (finishTimeUnit, _) = m_DateTimeCalculator
                    .CalculateTimeAndDateTime(
                        vm.ProjectStart,
                        newFinishDate);
            }

            return Math.Max(1, (finishTimeUnit ?? startTimeUnit + 1) - startTimeUnit);
        }

        private static AnnotatedBar? HitTestBarRightEdge(
            AvaPlot plot,
            Pixel mousePixel)
        {
            BarPlot? barPlot = plot.Plot.GetPlottables<BarPlot>().FirstOrDefault();

            if (barPlot is null)
            {
                return null;
            }

            foreach (AnnotatedBar bar in barPlot.Bars.OfType<AnnotatedBar>())
            {
                if (bar.ActivityId == 0)
                {
                    continue;
                }

                Pixel rightEdgePixel = plot.Plot.GetPixel(new Coordinates(bar.Value, bar.Position));
                Pixel topPixel = plot.Plot.GetPixel(new Coordinates(bar.Value, bar.Position + 0.25));
                Pixel botPixel = plot.Plot.GetPixel(new Coordinates(bar.Value, bar.Position - 0.25));

                bool nearRightEdge = Math.Abs(mousePixel.X - rightEdgePixel.X) <= c_ResizeEdgePixelTolerance;
                bool withinBarY = mousePixel.Y >= Math.Min(topPixel.Y, botPixel.Y)
                               && mousePixel.Y <= Math.Max(topPixel.Y, botPixel.Y);

                if (nearRightEdge && withinBarY)
                {
                    return bar;
                }
            }

            return null;
        }
    }
}
