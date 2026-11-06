using Avalonia.Threading;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// The desktop's <see cref="IUIDispatcher"/>: Avalonia's UI thread, reached exactly as the view
    /// models used to reach it themselves.
    /// </summary>
    public sealed class AvaloniaUIDispatcher
        : IUIDispatcher
    {
        public async Task InvokeAsync(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            await Dispatcher.UIThread.InvokeAsync(action);
        }

        public void Defer(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);

            // Background priority runs after the input, data binding and render work already
            // queued, which is what "after the work already waiting" means on the UI thread.
            Dispatcher.UIThread.Post(action, DispatcherPriority.Background);
        }
    }
}
