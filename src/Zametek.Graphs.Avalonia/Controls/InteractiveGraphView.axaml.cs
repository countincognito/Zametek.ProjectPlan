using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SkiaSharp;

namespace Zametek.Graphs.Avalonia
{
    // Reusable, embeddable interactive graph viewer. It draws against any host that implements
    // IInteractiveGraph (e.g. the application's ArrowGraphManagerViewModel / VertexGraphManagerViewModel,
    // via their InteractiveGraphViewModel), supporting drag, click-to-select, hover tooltips,
    // unbounded pan and zoom. (Replaces the parallel InteractiveArrowGraphView/InteractiveVertexGraphView.)
    public partial class InteractiveGraphView
        : UserControl
    {
        // Pan is a render-transform translation (unbounded), not a scroll offset, so nodes
        // dragged anywhere remain reachable by panning. Zoom scales the content about its origin.
        private readonly TranslateTransform m_PanTransform = new();

        private bool m_IsPanning;
        private Point m_PanStart;
        private double m_PanStartX;
        private double m_PanStartY;
        private Point m_LastPointer;
        private bool m_HasCentered;
        // Suppresses SaveViewState while persisted values are pushed back into the slider/pan
        // transform, so the slider's own ValueChanged does not re-save an interim pan.
        private bool m_Restoring;
        // The interactive graph we have hooked ViewReset/GraphRefreshed on, tracked so the subscription
        // survives the data context resolving after attach (and is cleaned up exactly once).
        private IInteractiveGraph? m_ViewModelEventsSource;
        // Coalesces re-frame requests: only the most recently scheduled one actually frames, and it runs
        // at Background priority so it lands after the rebuild + seed have applied the final positions.
        private int m_FramingToken;
        private const double c_SliderDelta = 0.1;

        public InteractiveGraphView()
        {
            InitializeComponent();
            panLayer.RenderTransform = m_PanTransform;
        }

        private double Zoom => zoomer.Value;

        // When the control is rebuilt (e.g. a dock tab switch) restore the persisted framing as soon
        // as it attaches. If the data context is not bound yet, Graph_SizeChanged restores once it is.
        // The first ever framing (HasViewState false) is left to the auto-fit below.
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            // Hook the view-model events here and in OnDataContextChanged: whichever happens with the data
            // context bound wins (the {Binding Interactive} often resolves after attach), so neither the
            // live reset nor the re-frame is missed.
            HookViewModelEvents();
            TryFrameGraph();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            UnhookViewModelEvents();
            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            HookViewModelEvents();
        }

        // Subscribe to the current data context's ViewReset/GraphRefreshed (idempotent: re-points the
        // handlers when the data context changes, no-ops when it has not).
        private void HookViewModelEvents()
        {
            IInteractiveGraph? current = DataContext as IInteractiveGraph;
            if (ReferenceEquals(current, m_ViewModelEventsSource))
            {
                return;
            }

            UnhookViewModelEvents();

            if (current is not null)
            {
                current.ViewReset += OnViewReset;
                current.GraphRefreshed += OnGraphRefreshed;
                m_ViewModelEventsSource = current;
            }
        }

        private void UnhookViewModelEvents()
        {
            if (m_ViewModelEventsSource is not null)
            {
                m_ViewModelEventsSource.ViewReset -= OnViewReset;
                m_ViewModelEventsSource.GraphRefreshed -= OnGraphRefreshed;
                m_ViewModelEventsSource = null;
            }
        }

        // The graph was rebuilt or re-seeded. Schedule a re-frame (deferred + coalesced) so a fresh load
        // is framed even when the workspace size did not change (identical graph, different layout).
        private void OnGraphRefreshed(object? sender, EventArgs e) => ScheduleFraming();

        // Defer framing to Background priority and keep only the latest request, so it runs once after the
        // rebuild and the (higher-priority) seed have applied the final node positions.
        private void ScheduleFraming()
        {
            int token = ++m_FramingToken;
            Dispatcher.UIThread.Post(
                () =>
                {
                    if (token == m_FramingToken)
                    {
                        TryFrameGraph();
                    }
                },
                DispatcherPriority.Background);
        }

        // The project scenario was reset/closed: drop the live transform back to x1 / origin and let the
        // next graph re-frame from scratch (the view model has already cleared HasViewState).
        private void OnViewReset(object? sender, EventArgs e)
        {
            m_Restoring = true;
            try
            {
                zoomer.Value = 1.0;
                m_PanTransform.X = 0.0;
                m_PanTransform.Y = 0.0;
            }
            finally
            {
                m_Restoring = false;
            }
            m_HasCentered = false;
        }

        // Frame the graph the first time the viewport has a size and there are nodes; afterwards the
        // user's pan/zoom is preserved across re-layouts. Wired to both the viewport and the graph
        // canvas, because the viewport's size and the canvas's (binding-driven) workspace size can
        // settle in either order - the initial fit only needs the viewport bounds and the nodes, so it
        // must retry whenever either arrives. The auto-fit only shrinks to fit (it never enlarges a
        // small graph past its natural size). Once a framing has been persisted, restore it instead
        // (this is what survives the control being re-materialised on a tab switch).
        private void Graph_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            TryFrameGraph();
        }

        // Frame the graph once per load: restore the persisted framing if there is one, otherwise auto-fit.
        // No-op once framed (m_HasCentered), so it neither disturbs a later manual pan/zoom nor re-fits on
        // every recompile. ResetView clears m_HasCentered so the next load frames afresh.
        private void TryFrameGraph()
        {
            if (m_HasCentered)
            {
                return;
            }

            if (DataContext is IInteractiveGraph viewModel && viewModel.HasViewState)
            {
                RestoreViewState(viewModel);
                m_HasCentered = true;
                return;
            }

            if (FitToView(maxZoom: 1.0))
            {
                m_HasCentered = true;
            }
        }

        // Pan so a workspace of the given size sits centred in the viewport at the current zoom.
        private void CentreWorkspace(double workspaceWidth, double workspaceHeight)
        {
            if (workspaceWidth <= 0
                || workspaceHeight <= 0
                || viewport.Bounds.Width <= 0
                || viewport.Bounds.Height <= 0)
            {
                return;
            }

            m_PanTransform.X = (viewport.Bounds.Width - (workspaceWidth * Zoom)) / 2.0;
            m_PanTransform.Y = (viewport.Bounds.Height - (workspaceHeight * Zoom)) / 2.0;
            SaveViewState();
        }

        private void Viewport_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // A node press is already handled (and selects/drags); ignore those here.
            if (e.Handled)
            {
                return;
            }

            PointerPoint point = e.GetCurrentPoint(viewport);
            if (!point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            // Pressing empty space clears the selection and begins a pan.
            (DataContext as IInteractiveGraph)?.SelectNode(null);

            m_IsPanning = true;
            m_PanStart = point.Position;
            m_PanStartX = m_PanTransform.X;
            m_PanStartY = m_PanTransform.Y;
            viewport.Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Pointer.Capture(viewport);
        }

        private void Viewport_PointerMoved(object? sender, PointerEventArgs e)
        {
            m_LastPointer = e.GetPosition(viewport);

            if (!m_IsPanning)
            {
                return;
            }

            m_PanTransform.X = m_PanStartX + (m_LastPointer.X - m_PanStart.X);
            m_PanTransform.Y = m_PanStartY + (m_LastPointer.Y - m_PanStart.Y);
        }

        private void Viewport_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!m_IsPanning)
            {
                return;
            }

            m_IsPanning = false;
            viewport.Cursor = new Cursor(StandardCursorType.Arrow);
            e.Pointer.Capture(null);
            SaveViewState();
        }

        private void Zoom_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            m_LastPointer = e.GetPosition(viewport);

            if (e.Delta.Y > 0)
            {
                zoomer.Value += c_SliderDelta;
            }
            if (e.Delta.Y < 0)
            {
                zoomer.Value -= c_SliderDelta;
            }

            e.Handled = true;
        }

        private void Slider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            double oldZoom = e.OldValue;
            double newZoom = e.NewValue;
            if (oldZoom <= 0.0)
            {
                return;
            }

            // Keep the point under the cursor fixed as the zoom changes.
            double factor = newZoom / oldZoom;
            m_PanTransform.X = m_LastPointer.X - (factor * (m_LastPointer.X - m_PanTransform.X));
            m_PanTransform.Y = m_LastPointer.Y - (factor * (m_LastPointer.Y - m_PanTransform.Y));
            SaveViewState();
        }

        // Frame every node in the viewport: pick the zoom that fits the node bounding box (with a
        // margin) and pan so it is centred. This recovers any node that has been dragged far away.
        // The explicit menu command fills the viewport (zooming in if the graph is small).
        private void FitToView_Click(object? sender, RoutedEventArgs e)
        {
            FitToView(maxZoom: zoomer.Maximum);
        }

        // Pick the zoom that frames the node bounding box (with a margin) in the viewport and pan so
        // it is centred, capping the zoom at maxZoom (the auto-fit passes 1.0 so it only shrinks to
        // fit; the menu command passes the slider maximum so it also fills a small graph). Returns
        // false without changing anything when there is nothing to frame yet (no nodes, or the
        // viewport has no size).
        private bool FitToView(double maxZoom)
        {
            if (DataContext is not IInteractiveGraph viewModel
                || viewModel.GraphNodes.Count == 0
                || viewport.Bounds.Width <= 0
                || viewport.Bounds.Height <= 0)
            {
                return false;
            }

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;
            foreach (GraphNodeViewModel node in viewModel.GraphNodes)
            {
                minX = Math.Min(minX, node.X);
                minY = Math.Min(minY, node.Y);
                maxX = Math.Max(maxX, node.X + node.Width);
                maxY = Math.Max(maxY, node.Y + node.Height);
            }

            const double fitMargin = 40.0;
            double contentWidth = (maxX - minX) + (2.0 * fitMargin);
            double contentHeight = (maxY - minY) + (2.0 * fitMargin);

            double zoom = Math.Min(
                viewport.Bounds.Width / contentWidth,
                viewport.Bounds.Height / contentHeight);
            zoom = Math.Clamp(zoom, zoomer.Minimum, Math.Min(zoomer.Maximum, maxZoom));
            zoomer.Value = zoom;

            // Centre the content (Slider_ValueChanged may have nudged the pan, so set it last).
            double contentCentreX = (minX + maxX) / 2.0;
            double contentCentreY = (minY + maxY) / 2.0;
            m_PanTransform.X = (viewport.Bounds.Width / 2.0) - (contentCentreX * zoom);
            m_PanTransform.Y = (viewport.Bounds.Height / 2.0) - (contentCentreY * zoom);
            SaveViewState();
            return true;
        }

        // Discard all dragged positions, rebuild the default MSAGL layout, and reproduce the
        // first-compilation framing (default zoom, centred).
        private void ResetLayout_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not IInteractiveGraph viewModel)
            {
                return;
            }

            viewModel.ResetLayout();
            CentreWorkspace(viewModel.WorkspaceWidth, viewModel.WorkspaceHeight);
        }

        // Persist the current zoom + pan into the view model so the framing survives the control being
        // rebuilt (e.g. a dock tab switch). No-op while restoring (the slider's ValueChanged would
        // otherwise re-save an interim pan) or before the data context is bound.
        private void SaveViewState()
        {
            if (m_Restoring
                || DataContext is not IInteractiveGraph viewModel)
            {
                return;
            }

            viewModel.ViewZoom = zoomer.Value;
            viewModel.ViewPanX = m_PanTransform.X;
            viewModel.ViewPanY = m_PanTransform.Y;
            viewModel.HasViewState = true;
        }

        // Push the persisted zoom + pan back into the slider and pan transform. The zoom is applied
        // first (its ValueChanged nudges the pan), then the saved pan overwrites it; SaveViewState is
        // suppressed throughout so the interim pan is never persisted.
        private void RestoreViewState(IInteractiveGraph viewModel)
        {
            m_Restoring = true;
            try
            {
                zoomer.Value = viewModel.ViewZoom;
                m_PanTransform.X = viewModel.ViewPanX;
                m_PanTransform.Y = viewModel.ViewPanY;
            }
            finally
            {
                m_Restoring = false;
            }
        }

        // Copy the whole graph (the bounding-box-cropped render, matching the Save-Image export, not the
        // current viewport) to the clipboard as an image. Built defensively for cross-platform use: the
        // payload offers both the native bitmap representation (preferred where the backend supports it)
        // and the raw image/png bytes (broadly readable, e.g. on X11/Wayland), so the OS picks whichever
        // it understands. The whole operation is best-effort - if a clipboard backend cannot accept an
        // image it fails silently rather than crashing, and the Save-Image export remains the guaranteed
        // path.
        private async void CopyImage_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (DataContext is not IInteractiveGraph viewModel)
                {
                    return;
                }

                IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard is null)
                {
                    return;
                }

                using SKPicture? picture = InteractiveGraphRenderer.Render(viewModel.GraphNodes, viewModel.GraphEdges);
                if (picture is null)
                {
                    return;
                }

                byte[] png = await ImageExporter.RenderToPngAsync(picture);

                DataTransfer? dataTransfer = BuildImageDataTransfer(png);
                if (dataTransfer is null)
                {
                    return;
                }

                await clipboard.SetDataAsync(dataTransfer);
            }
            catch
            {
                // Best-effort: never crash the app if a clipboard backend cannot accept an image.
            }
        }

        // Assemble a single clipboard item carrying the image in two representations - the platform's
        // native bitmap and raw image/png bytes - so the receiving app can choose. Each representation
        // is added independently; if one cannot be produced on this platform/build the other still
        // stands. Returns null only when neither could be added.
        private static DataTransfer? BuildImageDataTransfer(byte[] png)
        {
            var item = new DataTransferItem();
            bool added = false;

            // Native bitmap (preferred where supported - Avalonia writes the platform image format).
            // The bitmap must not be disposed: Avalonia owns the lifetime of anything handed to the
            // clipboard.
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

            // Raw PNG bytes under image/png (broadly readable, especially on Linux/X11/Wayland), as a
            // second representation on the same item so the receiving app can choose.
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
