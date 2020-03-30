using System;

namespace nGantt.GanttChart
{
    public class PeriodEventArgs : EventArgs
    {
        public DateTime SelectionStart { get; set; }
        public DateTime SelectionEnd { get; set; }
    }
}
