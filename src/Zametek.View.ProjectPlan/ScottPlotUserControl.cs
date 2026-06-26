using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Zametek.Contract.ProjectPlan;
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
            ClosePlotContainerContextMenu();

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
            if (m_PlotContainer?.ContextMenu is not ContextMenu menu)
            {
                return;
            }

            menu.Open(m_PlotContainer);
            // Mark as handled to prevent ScottPlot from doing anything else
            e.Handled = true;

            // ScottPlot's AvaPlot captures pointer events, so ContextMenu's built-in
            // light-dismiss never sees outside clicks. Watch at the TopLevel in the
            // tunnel phase with handledEventsToo so the click is observed regardless
            // of who marked it handled.
            TopLevel? topLevel = TopLevel.GetTopLevel(m_PlotContainer);
            if (topLevel is null)
            {
                return;
            }

            void OnTopLevelPointerPressed(object? _, PointerPressedEventArgs args)
            {
                if (args.Source is Visual src
                    && src.FindAncestorOfType<ContextMenu>(includeSelf: true) == menu)
                {
                    return;
                }
                menu.Close();
            }

            void OnMenuClosed(object? _, RoutedEventArgs args)
            {
                topLevel.RemoveHandler(PointerPressedEvent, OnTopLevelPointerPressed);
                menu.Closed -= OnMenuClosed;
            }

            topLevel.AddHandler(
                PointerPressedEvent,
                OnTopLevelPointerPressed,
                RoutingStrategies.Tunnel,
                handledEventsToo: true);

            menu.Closed += OnMenuClosed;
        }

        private void ClosePlotContainerContextMenu()
        {
            m_PlotContainer?.ContextMenu?.Close();
            //if (m_PlotContainer?.ContextMenu is not null)
            //{
            //    m_PlotContainer.ContextMenu.Close();
            //}
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

        // Copy a PNG image to the clipboard, best-effort and defensively for cross-platform use: the
        // payload carries both the native bitmap (preferred where supported) and the raw image/png bytes
        // (broadly readable, e.g. on X11/Wayland). Shared by the ScottPlot charts (Gantt, resource,
        // earned-value); it never throws if a backend cannot accept an image (Save-As remains the
        // guaranteed fallback).
        protected async Task CopyImageToClipboardAsync()
        {
            if (DataContext is not IScottPlotViewModel vm)
            {
                return;
            }

            try
            {
                byte[]? png = await vm.RenderChartImageAsync();

                if (png is null || png.Length == 0)
                {
                    return;
                }

                IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard is null)
                {
                    return;
                }

                DataTransfer? dataTransfer = BuildImageDataTransfer(png);
                if (dataTransfer is null)
                {
                    await vm.ReportErrorAsync(Resource.ProjectPlan.Messages.Message_ClipboardCopyFailed);
                    return;
                }

                await clipboard.SetDataAsync(dataTransfer);
            }
            catch
            {
                // Best-effort: never crash if a clipboard backend cannot accept an image.
                await vm.ReportErrorAsync(Resource.ProjectPlan.Messages.Message_ClipboardCopyFailed);
            }
        }

        private static DataTransfer? BuildImageDataTransfer(byte[] png)
        {
            var item = new DataTransferItem();
            bool added = false;

            // Native bitmap (preferred where supported). Avalonia owns anything handed to the clipboard,
            // so the bitmap must not be disposed here.
            try
            {
                var bitmap = new Bitmap(new MemoryStream(png));
                item.SetBitmap(bitmap);
                added = true;
            }
            catch
            {
                // Bitmap representation unavailable on this platform/build; rely on the raw bytes below.
            }

            // Raw PNG bytes under image/png (broadly readable), as a second representation on the same item.
            try
            {
                item.Set(DataFormat.CreateBytesPlatformFormat("image/png"), png);
                added = true;
            }
            catch
            {
                // image/png byte format unavailable; rely on the bitmap representation above (if any).
            }

            if (!added)
            {
                return null;
            }

            var dataTransfer = new DataTransfer();
            dataTransfer.Add(item);
            return dataTransfer;
        }
    }
}
