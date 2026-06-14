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
        private bool m_HasMoved;
        private Point m_StartPointer;
        private double m_StartX;
        private double m_StartY;
        private Canvas? m_Canvas;
        private VertexGraphManagerViewModel? m_Manager;

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
            m_Manager = AssociatedObject.FindAncestorOfType<ItemsControl>()?.DataContext as VertexGraphManagerViewModel;
            m_Manager?.SelectNode(node);

            m_Canvas = AssociatedObject.FindAncestorOfType<Canvas>();
            if (m_Canvas is null)
            {
                return;
            }

            m_StartPointer = e.GetPosition(m_Canvas);
            m_StartX = node.X;
            m_StartY = node.Y;
            m_IsDragging = true;
            m_HasMoved = false;
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
            m_HasMoved = true;
            e.Handled = true;
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!m_IsDragging)
            {
                return;
            }

            // Only remember a position if the node was actually dragged, so a plain
            // click selects without pinning the node against future re-layouts.
            if (m_HasMoved
                && AssociatedObject?.DataContext is VertexGraphNodeViewModel node)
            {
                m_Manager?.OnNodeMoved(node);
            }

            m_IsDragging = false;
            m_HasMoved = false;
            m_Canvas = null;
            m_Manager = null;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }
}
