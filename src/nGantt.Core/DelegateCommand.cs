using System;
using System.Windows.Input;

namespace nGantt
{
    public class DelegateCommand<T> : ICommand
    {
        private readonly Action<T> execute;
        private readonly Predicate<T> canExecute;

        public DelegateCommand(Action<T> execute)
            : this(execute, null) { }

        public DelegateCommand(Action<T> execute, Predicate<T> canExecute)
        {
            if (execute == null)
                throw new ArgumentNullException(nameof(execute));

            this.execute = execute;
            this.canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return canExecute == null || canExecute((T)parameter);
        }
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        public void Execute(object theParameter)
        {
            execute((T)theParameter);
        }
    }
}
