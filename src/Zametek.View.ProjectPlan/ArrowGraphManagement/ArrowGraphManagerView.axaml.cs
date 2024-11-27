using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using System;

namespace Zametek.View.ProjectPlan
{
    // Much of the panning capability was taken from here:
    // https://www.codeproject.com/Articles/97871/WPF-simple-zoom-and-drag-support-in-a-ScrollViewer
    public partial class ArrowGraphManagerView
        : UserControl
    {
        private Point? m_LastDragPoint = new Point();
        private Point m_CurrentPoint = new();
        private const double c_SliderDelta = 0.1;

        public ArrowGraphManagerView()
        {
            InitializeComponent();
        }

        private void ScrollViewer_PointerMoved(object? sender, PointerEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer is not null)
            {
                Point posNow = e.GetPosition(scrollViewer);
                m_CurrentPoint = posNow;

                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    if (m_LastDragPoint.HasValue)
                    {
                        double dX = posNow.X - m_LastDragPoint.Value.X;
                        double dY = posNow.Y - m_LastDragPoint.Value.Y;
                        m_LastDragPoint = posNow;
                        scrollViewer.Offset = new Vector(scrollViewer.Offset.X - dX, scrollViewer.Offset.Y - dY);
                    }
                }
            }
        }

        private void ScrollViewer_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer is not null
                && e.InitialPressMouseButton == MouseButton.Left)
            {
                scrollViewer.Cursor = new Cursor(StandardCursorType.Arrow);
                m_LastDragPoint = null;
            }
        }

        private void ScrollViewer_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer is not null
                && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                Point pointer = e.GetPosition(scrollViewer);
                if (pointer.X <= scrollViewer.Viewport.Width
                    && pointer.Y < scrollViewer.Viewport.Height) //make sure we still can use the scrollbars
                {
                    scrollViewer.Cursor = new Cursor(StandardCursorType.SizeAll);
                    m_LastDragPoint = pointer;
                }
            }
        }

        private void Zoom_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);

            // Centering of the zoom will be handled by the slider ValueChanged event.
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
            ArgumentNullException.ThrowIfNull(e);
            double oldZoom = e.OldValue;
            Vector oldOffset = viewer.Offset;
            double newZoom = e.NewValue;

            // This centers the zoom according to the last position of the mouse pointer.
            Vector newOffset = CalculateOffsetVector(oldOffset, oldZoom, newZoom);
            viewer.Offset = newOffset;
        }

        private Vector CalculateOffsetVector(Vector oldOffset, double oldZoom, double newZoom)
        {
            // Reposition the scrollviewer to center the image zoom on the mouse pointer.
            double factor = newZoom / oldZoom;

            return new Vector(
                (oldOffset.X + m_CurrentPoint.X) * factor - m_CurrentPoint.X,
                (oldOffset.Y + m_CurrentPoint.Y) * factor - m_CurrentPoint.Y);
        }
    }
}
