using System.Collections.ObjectModel;

namespace nGantt.GanttChart
{
    public class GanttRowGroup
    {
        public GanttRowGroup()
        {
            Rows = new ObservableCollection<GanttRow>();
        }
        public ObservableCollection<GanttRow> Rows { get; set; }
    }
}
