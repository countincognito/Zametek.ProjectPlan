using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using System;

namespace Zametek.View.ProjectPlan
{
    // Much of the panning capability was taken from here:
    // https://www.codeproject.com/Articles/97871/WPF-simple-zoom-and-drag-support-in-a-ScrollViewer
    public partial class ArrowGraphManagerView
        : UserControl
    {
        private Point? m_LastDragPoint;

        public ArrowGraphManagerView()
        {
            InitializeComponent();
        }

        private void ScrollViewer_PointerMoved(object? sender, PointerEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer is not null
                && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                if (m_LastDragPoint.HasValue)
                {
                    Point posNow = e.GetPosition(scrollViewer);
                    double dX = posNow.X - m_LastDragPoint.Value.X;
                    double dY = posNow.Y - m_LastDragPoint.Value.Y;
                    m_LastDragPoint = posNow;
                    scrollViewer.Offset = new Vector(scrollViewer.Offset.X - dX, scrollViewer.Offset.Y - dY);
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
                var mousePos = e.GetPosition(scrollViewer);
                if (mousePos.X <= scrollViewer.Viewport.Width
                    && mousePos.Y < scrollViewer.Viewport.Height) //make sure we still can use the scrollbars
                {
                    scrollViewer.Cursor = new Cursor(StandardCursorType.SizeAll);
                    m_LastDragPoint = mousePos;
                }
            }
        }

        private void Slider_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);
            var slider = sender as Slider;
            if (slider is not null)
            {
                if (e.Delta.Y > 0)
                {
                    slider.Value += 0.5;
                }
                if (e.Delta.Y < 0)
                {
                    slider.Value -= 0.5;
                }
                e.Handled = true;
            }
        }
    }
}
