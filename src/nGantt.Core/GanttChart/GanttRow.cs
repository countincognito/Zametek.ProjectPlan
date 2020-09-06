using System.Collections.ObjectModel;

namespace nGantt.GanttChart
{
    public class GanttRow
    {
        public GanttRowHeader RowHeader { get; set; }
        public ObservableCollection<GanttTask> Tasks { get; set; }
        public bool HasErrors { get; set; }
    }
}
