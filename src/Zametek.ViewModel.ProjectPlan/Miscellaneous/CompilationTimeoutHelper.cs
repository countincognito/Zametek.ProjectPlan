using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// The watchdog around a graph compilation. The compilers take a cancellation
    /// token and check it between pipeline phases and inside resource scheduling,
    /// so a compilation that stops making progress can be abandoned instead of
    /// hanging the application on it.
    /// </summary>
    /// <remarks>
    /// The budget comes from ISettingService.CompilationTimeoutMilliseconds, and
    /// zero or less switches it off. Every call site reads the setting once into a
    /// local and passes it to both methods here, so the token that fired and the
    /// message that reports it always describe the same budget, even if the setting
    /// changes mid-compile.
    /// <para>
    /// Note that a budget of a few milliseconds is not meaningful: the underlying
    /// timer only guarantees to fire at or after the delay, and its resolution is
    /// coarser than that, so a very short budget may not be signalled before an
    /// ordinary compilation has already finished.
    /// </para>
    /// </remarks>
    public static class CompilationTimeoutHelper
    {
        /// <summary>
        /// Creates the watchdog for a single compilation, or null when no limit
        /// applies. Create it as late as possible - immediately around the compile -
        /// so that time spent waiting on a lock is not charged to the budget.
        /// </summary>
        public static CancellationTokenSource? CreateTimeoutSource(int timeoutMilliseconds)
        {
            return timeoutMilliseconds <= 0
                ? null
                : new CancellationTokenSource(timeoutMilliseconds);
        }

        /// <summary>
        /// The token to hand to the compiler, which is <see cref="CancellationToken.None"/>
        /// when no limit applies.
        /// </summary>
        public static CancellationToken TokenOrNone(CancellationTokenSource? timeoutSource)
        {
            return timeoutSource?.Token ?? CancellationToken.None;
        }

        /// <summary>
        /// Builds the exception that replaces the compiler's bare
        /// <see cref="OperationCanceledException"/>, so that the view model which
        /// started the work can report a timeout rather than a cancellation.
        /// </summary>
        public static GraphCompilationTimeoutException TimedOut(
            int timeoutMilliseconds,
            Exception innerException)
        {
            return new GraphCompilationTimeoutException(
                string.Format(Resource.ProjectPlan.Messages.Message_CompilationTimedOut, timeoutMilliseconds),
                TimeSpan.FromMilliseconds(timeoutMilliseconds),
                innerException);
        }
    }
}
