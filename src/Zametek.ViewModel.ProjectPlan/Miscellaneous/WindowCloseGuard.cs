using System.ComponentModel;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Decides what happens to each request to close the main window. The window asks before it closes a project that
    /// has changes, and a handler of the Closing event cannot wait for the answer. So the guard cancels each request until
    /// the answer is yes, and then it closes the window itself.
    /// </summary>
    /// <remarks>
    /// One decision is open at a time. A request that comes while a decision is open is cancelled, and it starts nothing:
    /// the end of the Windows session during the question does not close the window without the answer. The decision
    /// first waits for a write that is in progress (a save or an export), so that the process does not end in the middle
    /// of the write. Then it reads the changes, and it asks only when the project has some. A failure of the question or
    /// of the close is reported, and the window stays open.
    ///
    /// The window calls <see cref="ShouldCancel"/> on the UI thread, and the decision continues there. The first part of a
    /// decision runs inside that call: with no write in progress and no changes, the close is called before the call
    /// returns. That close is a request too, and it is the one that the guard lets through.
    /// </remarks>
    public sealed class WindowCloseGuard
    {
        private readonly Func<bool> m_IsWriting;
        private readonly INotifyPropertyChanged m_WritingSource;
        private readonly Func<bool> m_HasChanges;
        private readonly Func<Task<bool>> m_ConfirmCloseAsync;
        private readonly Action m_Close;
        private readonly Action<Exception> m_ReportFailure;

        private int m_IsDeciding;
        private int m_IsAllowed;
        private Task m_LastDecision = Task.CompletedTask;

        /// <param name="isWriting">True while a save or an export is in progress.</param>
        /// <param name="writingSource">Raises PropertyChanged when the value of <paramref name="isWriting"/> can be another one.</param>
        /// <param name="hasChanges">True when the project has changes that are not saved.</param>
        /// <param name="confirmCloseAsync">Asks if the window must close although the project has changes. True for yes.</param>
        /// <param name="close">Closes the window. The window then asks the guard again, and the guard lets that request through.</param>
        /// <param name="reportFailure">Gets an exception from the question or from the close.</param>
        public WindowCloseGuard(
            Func<bool> isWriting,
            INotifyPropertyChanged writingSource,
            Func<bool> hasChanges,
            Func<Task<bool>> confirmCloseAsync,
            Action close,
            Action<Exception> reportFailure)
        {
            ArgumentNullException.ThrowIfNull(isWriting);
            ArgumentNullException.ThrowIfNull(writingSource);
            ArgumentNullException.ThrowIfNull(hasChanges);
            ArgumentNullException.ThrowIfNull(confirmCloseAsync);
            ArgumentNullException.ThrowIfNull(close);
            ArgumentNullException.ThrowIfNull(reportFailure);
            m_IsWriting = isWriting;
            m_WritingSource = writingSource;
            m_HasChanges = hasChanges;
            m_ConfirmCloseAsync = confirmCloseAsync;
            m_Close = close;
            m_ReportFailure = reportFailure;
        }

        /// <summary>
        /// The decision that the last request started. It ends when the window closes, or when it is known that the window
        /// stays open. For tests.
        /// </summary>
        internal Task LastDecision => Volatile.Read(ref m_LastDecision);

        /// <summary>
        /// Call this for each Closing event of the window. True when the event must be cancelled.
        /// </summary>
        public bool ShouldCancel()
        {
            if (Volatile.Read(ref m_IsAllowed) == 1)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref m_IsDeciding, 1, 0) == 0)
            {
                Volatile.Write(ref m_LastDecision, DecideAsync());
            }

            return true;
        }

        private async Task DecideAsync()
        {
            try
            {
                await WaitForWritesAsync();

                if (!m_HasChanges()
                    || await m_ConfirmCloseAsync())
                {
                    Volatile.Write(ref m_IsAllowed, 1);

                    try
                    {
                        m_Close();
                    }
                    catch
                    {
                        // The yes was for a close that did not happen. A later request asks again.
                        Volatile.Write(ref m_IsAllowed, 0);
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                // Nothing awaits a decision. An exception that left it would end the process.
                m_ReportFailure(ex);
            }
            finally
            {
                Volatile.Write(ref m_IsDeciding, 0);
            }
        }

        private Task WaitForWritesAsync()
        {
            if (!m_IsWriting())
            {
                return Task.CompletedTask;
            }

            // The source can raise its event on any thread, and under a lock of its own. So the decision continues after
            // the event, and not inside it.
            var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
            {
                if (!m_IsWriting())
                {
                    m_WritingSource.PropertyChanged -= OnPropertyChanged;
                    ended.TrySetResult();
                }
            }

            m_WritingSource.PropertyChanged += OnPropertyChanged;

            // The write can end between the first read and the subscription.
            OnPropertyChanged(m_WritingSource, new PropertyChangedEventArgs(null));

            return ended.Task;
        }
    }
}
