using Prism.Commands;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ApplicationCommands
        : IApplicationCommands
    {
        public CompositeCommand UndoCommand { get; } = new CompositeCommand(false);

        public CompositeCommand RedoCommand { get; } = new CompositeCommand(false);
    }
}
