using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    // Spike: lets the user drag an interactive vertex-graph node and select it on press.
    // Pointer positions are taken relative to the hosting Canvas, so dragging stays correct
    // under the surrounding zoom transform. e.Handled is set so the press does not also pan
    // the ScrollViewer behind the graph.
    public class NodeDragBehavior
        : Behavior<Control>
    {
        private bool m_IsDragging;
        private Point m_StartPointer;
        private double m_StartX;
        private double m_StartY;
        private Canvas? m_Canvas;

        protected override void OnAttached()
        {
            base.OnAttached();
            if (AssociatedObject is not null)
            {
                AssociatedObject.PointerPressed += OnPointerPressed;
                AssociatedObject.PointerMoved += OnPointerMoved;
                AssociatedObject.PointerReleased += OnPointerReleased;
            }
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject is not null)
            {
                AssociatedObject.PointerPressed -= OnPointerPressed;
                AssociatedObject.PointerMoved -= OnPointerMoved;
                AssociatedObject.PointerReleased -= OnPointerReleased;
            }
            base.OnDetaching();
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (AssociatedObject?.DataContext is not VertexGraphNodeViewModel node)
            {
                return;
            }
            if (!e.GetCurrentPoint(AssociatedObject).Properties.IsLeftButtonPressed)
            {
                return;
            }

            // Selection highlighting is owned by the manager view-model.
            if (AssociatedObject.FindAncestorOfType<ItemsControl>()?.DataContext is VertexGraphManagerViewModel manager)
            {
                manager.SelectNode(node);
            }

            m_Canvas = AssociatedObject.FindAncestorOfType<Canvas>();
            if (m_Canvas is null)
            {
                return;
            }

            m_StartPointer = e.GetPosition(m_Canvas);
            m_StartX = node.X;
            m_StartY = node.Y;
            m_IsDragging = true;
            e.Pointer.Capture(AssociatedObject);
            e.Handled = true;
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!m_IsDragging
                || m_Canvas is null
                || AssociatedObject?.DataContext is not VertexGraphNodeViewModel node)
            {
                return;
            }

            Point current = e.GetPosition(m_Canvas);
            node.X = m_StartX + (current.X - m_StartPointer.X);
            node.Y = m_StartY + (current.Y - m_StartPointer.Y);
            e.Handled = true;
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!m_IsDragging)
            {
                return;
            }

            m_IsDragging = false;
            m_Canvas = null;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }
}
