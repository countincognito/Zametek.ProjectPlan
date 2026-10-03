namespace Zametek.Contract.ProjectPlan
{
    /// <summary>
    /// A view model whose reactive pipelines start when it is told to, not when it is built.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A constructor sets up state and nothing that can call back. A subscription that delivers on another thread
    /// - the thread pool, or the UI thread - can run before the constructor has returned, on an object that is not
    /// yet whole, and in a host without a UI thread it runs on the thread pool whoever built the view model. So
    /// such subscriptions are made by <see cref="StartSubscriptions"/>. A host that wants the pipelines (the
    /// desktop and the browser) starts each view model as soon as its container has built it. A host that drives
    /// every step itself (the headless engine) starts none, and has nothing running that it did not ask for. A
    /// view model that makes others for itself starts them when it is started, or as it makes them if it already
    /// has been.
    /// </para>
    /// <para>
    /// Starting happens once: a second call does nothing, and nor does a call after
    /// <see cref="IKillSubscriptions.KillSubscriptions"/> or disposal. Killing a view model that was never started
    /// is safe. Each pipeline delivers its current value as it starts, as it did when it was made by the
    /// constructor.
    /// </para>
    /// </remarks>
    public interface IStartSubscriptions
    {
        void StartSubscriptions();
    }
}
