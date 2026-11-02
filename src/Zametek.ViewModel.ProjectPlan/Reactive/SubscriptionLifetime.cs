namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Where a view model is in the life of its reactive pipelines: made, started, or killed. It says whether a
    /// start should go ahead, and whether the view models an owner makes for itself should be started as they are
    /// made. Starting and killing are the host's and the owner's to order; they are not made safe against each
    /// other running at the same time.
    /// </summary>
    internal sealed class SubscriptionLifetime
    {
        private const int c_Made = 0;
        private const int c_Started = 1;
        private const int c_Killed = 2;

        private int m_State = c_Made;

        /// <summary>
        /// Whether the pipelines have started and not been killed.
        /// </summary>
        public bool IsStarted => Volatile.Read(ref m_State) == c_Started;

        /// <summary>
        /// Whether a start should go ahead: true for the first call on a view model that has been neither started
        /// nor killed, false for every other.
        /// </summary>
        public bool TryStart() => Interlocked.CompareExchange(ref m_State, c_Started, c_Made) == c_Made;

        /// <summary>
        /// Ends the life of the pipelines, whether or not they started: a start after this does nothing.
        /// </summary>
        public void Kill() => Volatile.Write(ref m_State, c_Killed);
    }
}
