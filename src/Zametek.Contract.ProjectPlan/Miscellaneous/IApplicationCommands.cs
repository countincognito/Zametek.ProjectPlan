using Prism.Commands;

namespace Zametek.Contract.ProjectPlan
{
    public interface IApplicationCommands
    {
        CompositeCommand UndoCommand { get; }

        CompositeCommand RedoCommand { get; }
    }
}
