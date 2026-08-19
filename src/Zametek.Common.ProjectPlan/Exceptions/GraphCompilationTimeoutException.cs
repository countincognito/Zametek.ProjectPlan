namespace Zametek.Common.ProjectPlan
{
    /// <summary>
    /// Thrown when a graph compilation is abandoned because it ran past its time
    /// budget. The compilers take a cancellation token and surface cancellation as
    /// an <see cref="OperationCanceledException"/>; this type distinguishes "the
    /// watchdog fired" from any other cancellation, so the code that catches it can
    /// say something useful rather than reporting a bare cancellation.
    /// <para>
    /// It is deliberately allowed to propagate out of the compile and build paths
    /// to whichever view model started the work, because those already report
    /// failures through the dialog service. The budget itself comes from
    /// ISettingService.CompilationTimeoutMilliseconds.
    /// </para>
    /// </summary>
    public class GraphCompilationTimeoutException
        : Exception
    {
        public GraphCompilationTimeoutException()
        {
        }

        public GraphCompilationTimeoutException(string message)
            : base(message)
        {
        }

        public GraphCompilationTimeoutException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public GraphCompilationTimeoutException(
            string message,
            TimeSpan timeout,
            Exception innerException)
            : base(message, innerException)
        {
            Timeout = timeout;
        }

        /// <summary>
        /// The budget that was exceeded.
        /// </summary>
        public TimeSpan Timeout { get; init; }
    }
}
