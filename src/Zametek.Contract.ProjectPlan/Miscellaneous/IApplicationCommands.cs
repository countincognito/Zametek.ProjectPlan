using Prism.Commands;

namespace Zametek.Contract.ProjectPlan
{
    public interface IApplicationCommands
    {
        DelegateCommandBase UndoCommand { get; set; }

        DelegateCommandBase RedoCommand { get; set; }
    }
}
