using Prism.Commands;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ApplicationCommands
        : IApplicationCommands
    {
        public DelegateCommandBase UndoCommand { get; set; }

        public DelegateCommandBase RedoCommand { get; set; }
    }
}
