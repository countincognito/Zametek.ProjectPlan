using System.Windows.Input;
using nGantt.PeriodSplitter;

namespace nGantt.GanttChart
{
    public delegate void SelectionContextMenuItemClick(Period selectedPeriod);

    public class SelectionContextMenuItem
    {
        public SelectionContextMenuItem(SelectionContextMenuItemClick contextMenuItemClick, string name)
        {
            ContextMenuItemClickCommand = new DelegateCommand<Period>(x => contextMenuItemClick(x));
            Name = name;
        }

        public string Name { get; set; }

        public ICommand ContextMenuItemClickCommand { get; private set; }
    }
}
