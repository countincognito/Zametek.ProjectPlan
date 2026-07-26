using System.Windows.Input;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// An immutable item in the Open Recent submenu: either a recently opened
    /// project file (header is the full path), the separator (header is "-",
    /// Avalonia's menu separator convention) or the clear command entry.
    /// </summary>
    public class RecentProjectFileMenuItemViewModel
        : IRecentProjectFileMenuItemViewModel
    {
        public RecentProjectFileMenuItemViewModel(
            string header,
            ICommand? command,
            object? commandParameter)
        {
            ArgumentNullException.ThrowIfNull(header);
            Header = header;
            Command = command;
            CommandParameter = commandParameter;
        }

        public string Header { get; }

        public ICommand? Command { get; }

        public object? CommandParameter { get; }
    }
}
