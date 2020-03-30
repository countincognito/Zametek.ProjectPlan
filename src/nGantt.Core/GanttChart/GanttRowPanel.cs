using System;
using System.Windows.Controls;
using System.Windows;

namespace nGantt.GanttChart
{
    public class GanttRowPanel : Panel 
    {
        public static readonly DependencyProperty StartDateProperty =
           DependencyProperty.RegisterAttached("StartDate", typeof(DateTime), typeof(GanttRowPanel), new FrameworkPropertyMetadata(DateTime.MinValue, FrameworkPropertyMetadataOptions.AffectsParentArrange));
        public static readonly DependencyProperty EndDateProperty =
            DependencyProperty.RegisterAttached("EndDate", typeof(DateTime), typeof(GanttRowPanel), new FrameworkPropertyMetadata(DateTime.MaxValue, FrameworkPropertyMetadataOptions.AffectsParentArrange));

        public static readonly DependencyProperty MaxDateProperty =
           DependencyProperty.Register("MaxDate", typeof(DateTime), typeof(GanttRowPanel), new FrameworkPropertyMetadata(DateTime.MaxValue, FrameworkPropertyMetadataOptions.AffectsMeasure));
        public static readonly DependencyProperty MinDateProperty =
            DependencyProperty.Register("MinDate", typeof(DateTime), typeof(GanttRowPanel), new FrameworkPropertyMetadata(DateTime.MinValue, FrameworkPropertyMetadataOptions.AffectsMeasure));


        public static DateTime GetStartDate(DependencyObject obj)
        {
            return (DateTime)obj.GetValue(StartDateProperty);
        }

        public static void SetStartDate(DependencyObject obj, DateTime value)
        {
            obj.SetValue(StartDateProperty, value);
        }

        public static DateTime GetEndDate(DependencyObject obj)
        {
            return (DateTime)obj.GetValue(EndDateProperty);
        }

        public static void SetEndDate(DependencyObject obj, DateTime value)
        {
            obj.SetValue(EndDateProperty, value);
        }

        public DateTime MaxDate
        {
            get { return (DateTime)GetValue(MaxDateProperty); }
            set { SetValue(MaxDateProperty, value); }
        }

        public DateTime MinDate
        {
            get { return (DateTime)GetValue(MinDateProperty); }
            set { SetValue(MinDateProperty, value); }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (UIElement child in Children)
                child.Measure(availableSize);

            return new Size(0, 0);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double range = (MaxDate - MinDate).Ticks;
            double pixelsPerTick = finalSize.Width / range;

            foreach (UIElement child in Children)
                ArrangeChild(child, MinDate, pixelsPerTick, finalSize.Height);

            return finalSize;
        }


        private void ArrangeChild(UIElement child, DateTime minDate, double pixelsPerTick, double elementHeight)
        {
            DateTime childStartDate = GetStartDate(child);
            DateTime childEndDate = GetEndDate(child);
            TimeSpan childDuration = childEndDate - childStartDate;

            double offset = (childStartDate - minDate).Ticks * pixelsPerTick;
            double width = childDuration.Ticks * pixelsPerTick;

            //if (width < 0)
            //{
            //    width = 0;
            //}

            if (offset < 0)
            {
                width = width + offset;
                offset = 0;
            }
            
            double range = (MaxDate - MinDate).Ticks;
            if ((offset + width) > range * pixelsPerTick)
                width = range * pixelsPerTick - offset;

            child.Arrange(new Rect(offset, 0, width, elementHeight));
        }
    }
}
