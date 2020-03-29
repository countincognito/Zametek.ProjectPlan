using System;
using System.Windows;
using System.Windows.Media;

namespace nGantt.GanttChart
{
    public class GanttTask : DependencyObject
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Name { get; set; }
        public Visibility TaskProgressVisibility { get; set; }
        public int Radius { get; set; }
        public SolidColorBrush BackgroundColor { get; set; }
        public SolidColorBrush ForegroundColor { get; set; }

        private double percentageCompleted;

        public GanttTask()
        {
            IsEnabled = true;
            TaskProgressVisibility = Visibility.Visible;
        }

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register("IsSelected", typeof(bool), typeof(GanttTask), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty IsEnabledProperty =
           DependencyProperty.Register("IsEnabled", typeof(bool), typeof(GanttTask), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public bool IsEnabled
        {
            get { return (bool)GetValue(IsEnabledProperty); }
            set { SetValue(IsEnabledProperty, value); }
        }

        public bool IsSelected
        {
            get { return (bool)GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }

        public double PercentageCompleted
        {
            get
            {
                return 1 - percentageCompleted;
            }
            set
            {
                percentageCompleted = value;
            }
        }

        public string PercentageCompletedText
        {
            get
            {
                return String.Format("{0}%", Math.Round(percentageCompleted * 100, 0));
            }
        }


    }
}
