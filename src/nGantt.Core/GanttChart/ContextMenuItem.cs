using System.Windows.Input;

namespace nGantt.GanttChart
{
    public delegate void ContextMenuItemClick(GanttTask ganttTask);

    public class ContextMenuItem
    {
        public ContextMenuItem(ContextMenuItemClick contextMenuItemClick, string name)
        {
            ContextMenuItemClickCommand = new DelegateCommand<GanttTask>(x => contextMenuItemClick(x));
            Name = name;
        }

        public string Name { get; set; }

        public ICommand ContextMenuItemClickCommand { get; private set; }
    }
}