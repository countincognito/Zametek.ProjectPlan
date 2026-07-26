using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface IRecentProjectFileMenuItemViewModel
    {
        string Header { get; }

        ICommand? Command { get; }

        object? CommandParameter { get; }
    }
}
