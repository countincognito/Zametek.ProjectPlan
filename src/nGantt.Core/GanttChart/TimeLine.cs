using System.Collections.ObjectModel;

namespace nGantt.GanttChart
{
    public class TimeLine
    {
        public TimeLine()
        {
            Items = new ObservableCollection<TimeLineItem>();
        }

        public ObservableCollection<TimeLineItem> Items { get; set; }

    }
}
