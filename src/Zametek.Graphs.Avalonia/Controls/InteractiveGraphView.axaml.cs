using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

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
        private const double c_SliderDelta = 0.1;

        public InteractiveGraphView()
        {
            InitializeComponent();
            panLayer.RenderTransform = m_PanTransform;
        }

        private double Zoom => zoomer.Value;

        // Frame the graph in the viewport the first time there are nodes and both have a size;
        // afterwards the user's pan/zoom is preserved across re-layouts. The auto-fit only shrinks
        // to fit (it never enlarges a small graph past its natural size), so the graph lands fully
        // on screen without surprising zoom-in.
        private void GraphCanvas_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (m_HasCentered)
            {
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
            zoomer.Value = 1.0;
            CentreWorkspace(viewModel.WorkspaceWidth, viewModel.WorkspaceHeight);
        }
    }
}
