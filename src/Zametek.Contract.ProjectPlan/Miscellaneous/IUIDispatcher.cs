namespace Zametek.Contract.ProjectPlan
{
    /// <summary>
    /// Where a view model's work runs when it cannot simply run where it was asked from. On the
    /// desktop that is the UI thread, because the work touches bound collections and commands that
    /// only the UI thread may touch. Headless there is no UI thread and nothing pumps one, so the
    /// work runs inline instead - which is also why this is an interface rather than a call to
    /// Dispatcher.UIThread: a headless job that posted to a dispatcher no one pumps would leave the
    /// work, and everything it holds, queued for the life of the process.
    /// </summary>
    public interface IUIDispatcher
    {
        /// <summary>
        /// Runs the action where such work belongs, and completes once it has.
        /// </summary>
        Task InvokeAsync(Action action);

        /// <summary>
        /// Runs the action after the work already waiting to run there. Nothing waits for it.
        /// </summary>
        void Defer(Action action);
    }
}
