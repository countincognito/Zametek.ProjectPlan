using ReactiveUI;
using System.Reactive;
using System.Threading;
using System.Windows.Input;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan
{
    public class SplashViewModel
        : ViewModelBase
    {
        public SplashViewModel()
        {
            ReactiveCommand<Unit, Unit> cancelCommand = ReactiveCommand.Create(Cancel);
            CancelCommand = cancelCommand;
        }

        private string m_StartUpMessage = Resource.ProjectPlan.Messages.Message_SplashScreenLoading;
        public string StartUpMessage
        {
            get => m_StartUpMessage;
            set
            {
                this.RaiseAndSetIfChanged(ref m_StartUpMessage, value);
            }
        }

        public ICommand CancelCommand { get; }

        private readonly CancellationTokenSource m_Cts = new();
        public CancellationToken CancellationToken => m_Cts.Token;

        public void Cancel()
        {
            m_Cts?.Cancel();
        }
    }
}
