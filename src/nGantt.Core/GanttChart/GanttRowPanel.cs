using System;
using System.Windows;
using System.Windows.Controls;

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


        public static DateTime GetStartDate(DependencyObject input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            return (DateTime)input.GetValue(StartDateProperty);
        }

        public static void SetStartDate(DependencyObject input, DateTime value)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            input.SetValue(StartDateProperty, value);
        }

        public static DateTime GetEndDate(DependencyObject input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            return (DateTime)input.GetValue(EndDateProperty);
        }

        public static void SetEndDate(DependencyObject input, DateTime value)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            input.SetValue(EndDateProperty, value);
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
