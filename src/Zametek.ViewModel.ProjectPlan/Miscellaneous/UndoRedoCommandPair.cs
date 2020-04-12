using System;
using System.Windows.Input;

namespace Zametek.ViewModel.ProjectPlan
{
    public class UndoRedoCommandPair
    {
        public UndoRedoCommandPair(
            ICommand undoCommand,
            object undoParameter,
            ICommand redoCommand,
            object redoParameter)
        {
            UndoCommand = undoCommand ?? throw new ArgumentNullException(nameof(undoCommand));
            UndoParameter = undoParameter;
            RedoCommand = redoCommand ?? throw new ArgumentNullException(nameof(redoCommand));
            RedoParameter = redoParameter;
        }

        public ICommand UndoCommand { get; }

        public object UndoParameter { get; }

        public ICommand RedoCommand { get; }

        public object RedoParameter { get; }
    }
}
