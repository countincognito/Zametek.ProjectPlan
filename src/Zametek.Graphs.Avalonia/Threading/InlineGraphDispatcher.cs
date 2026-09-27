namespace Zametek.Graphs.Avalonia
{
    // The IGraphDispatcher for a host with no UI thread: the work runs on the thread that asked for
    // it, before the call returns. Nothing is bound to the graph view-models there, so there is no
    // thread the work has to run on.
    public sealed class InlineGraphDispatcher
        : IGraphDispatcher
    {
        public void Invoke(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            action();
        }

        public void Post(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);

            // Nothing will run this later, so it runs now.
            action();
        }
    }
}
