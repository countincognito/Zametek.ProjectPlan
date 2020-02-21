using System;
using System.Collections.ObjectModel;

namespace nGantt.GanttChart
{
    public class GanttChartData
    {
        public GanttChartData()
        {
            RowGroups = new ObservableCollection<GanttRowGroup>();
            TimeLines = new ObservableCollection<TimeLine>();
        }
        public ObservableCollection<GanttRowGroup> RowGroups { get; set; }
        public ObservableCollection<TimeLine> TimeLines { get; set; }
        public DateTime MinDate { get; set; }
        public DateTime MaxDate { get; set; }
    }
}
