namespace Zametek.Graphs.Avalonia
{
    // Where the graph view-model's work runs when it cannot simply run where it was asked from: the
    // rebuild notification and the export both arrive off the UI thread, while populating the node
    // and edge view-models and rendering the live canvas may only happen on it. A host with a UI
    // uses AvaloniaGraphDispatcher, which is Avalonia's UI thread; a host without one (an automated
    // export, a server) uses InlineGraphDispatcher, because a dispatcher that nothing pumps would
    // hold the work - and the whole graph with it - for the life of the process.
    public interface IGraphDispatcher
    {
        // Runs the action where such work belongs, and returns once it has.
        void Invoke(Action action);

        // Runs the action later; nothing waits for it.
        void Post(Action action);
    }
}
