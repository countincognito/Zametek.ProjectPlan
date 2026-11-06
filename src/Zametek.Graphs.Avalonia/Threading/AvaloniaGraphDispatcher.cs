using Avalonia.Threading;

namespace Zametek.Graphs.Avalonia
{
    // The UI host's IGraphDispatcher, and the library's default: Avalonia's UI thread, reached
    // exactly as the graph view-model used to reach it itself.
    public sealed class AvaloniaGraphDispatcher
        : IGraphDispatcher
    {
        public void Invoke(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            Dispatcher.UIThread.Invoke(action);
        }

        public void Post(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            Dispatcher.UIThread.Post(action);
        }
    }
}
