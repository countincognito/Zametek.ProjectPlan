using System.Globalization;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// What the application does with an exception that nothing handled, on the UI thread or in a command or a
    /// subscription. The state of the application is not known after such an exception, and so the application does not go
    /// on. It writes the exception to the log, keeps a recovery copy of a project with changes, tells the user, and ends.
    /// </summary>
    /// <remarks>
    /// The host gives the parts: the copy, the dialog and the end. Thus, the handler has no window and no file of its own, and
    /// a test can run it. It does its work one time. An exception that comes while it works goes to the log. Exceptions that
    /// keep coming can keep the user from the message (a view that fails at each frame, for one), and so the application
    /// ends without the answer of the user after <see cref="MostExceptions"/> of them.
    /// </remarks>
    public sealed class UnhandledErrorHandler
    {
        /// <summary>
        /// The time that the handler waits for the recovery copy. The state that broke the application can also hold the
        /// lock that the copy needs.
        /// </summary>
        public static readonly TimeSpan CopyTimeLimit = TimeSpan.FromSeconds(15);

        /// <summary>
        /// The number of exceptions after which the application ends without the answer of the user.
        /// </summary>
        public const int MostExceptions = 5;

        private const int c_ExitCode = 1;

        private readonly Func<bool> m_HasChanges;
        private readonly Func<Task<RecoveryCopyModel?>> m_WriteCopyAsync;
        private readonly Func<string, Task> m_ShowErrorAsync;
        private readonly Action<int> m_End;
        private readonly Action<Exception, string> m_Log;
        private readonly TimeSpan m_CopyTimeLimit;
        private readonly Lock m_Lock = new();
        private readonly TaskCompletionSource m_Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int m_Exceptions;
        private bool m_HasEnded;

        /// <param name="hasChanges">Whether the open project has changes that no file of the user has.</param>
        /// <param name="writeCopyAsync">Writes the recovery copy, and gives null when it cannot.</param>
        /// <param name="showErrorAsync">Shows the message, and ends when the user closes it.</param>
        /// <param name="end">Ends the application with the exit code. The copy of this run stays.</param>
        /// <param name="log">Writes an exception to the log, with what the handler says of it.</param>
        /// <param name="copyTimeLimit">The time that the handler waits for the copy.</param>
        public UnhandledErrorHandler(
            Func<bool> hasChanges,
            Func<Task<RecoveryCopyModel?>> writeCopyAsync,
            Func<string, Task> showErrorAsync,
            Action<int> end,
            Action<Exception, string> log,
            TimeSpan? copyTimeLimit = null)
        {
            ArgumentNullException.ThrowIfNull(hasChanges);
            ArgumentNullException.ThrowIfNull(writeCopyAsync);
            ArgumentNullException.ThrowIfNull(showErrorAsync);
            ArgumentNullException.ThrowIfNull(end);
            ArgumentNullException.ThrowIfNull(log);
            m_HasChanges = hasChanges;
            m_WriteCopyAsync = writeCopyAsync;
            m_ShowErrorAsync = showErrorAsync;
            m_End = end;
            m_Log = log;
            m_CopyTimeLimit = copyTimeLimit ?? CopyTimeLimit;
        }

        /// <summary>
        /// Whether the application ends because of an exception. The close of the main window does not ask about unsaved
        /// changes then: the copy has them.
        /// </summary>
        public bool IsEnding
        {
            get
            {
                lock (m_Lock)
                {
                    return m_Exceptions > 0;
                }
            }
        }

        /// <summary>
        /// Ends when the handler has done its work: the application was told to end.
        /// </summary>
        public Task Completion => m_Completion.Task;

        /// <summary>
        /// Takes an exception that nothing handled. The first one starts the work of the handler. It does not wait for that
        /// work: the caller is the UI thread, which the message needs.
        /// </summary>
        /// <returns>True: the handler took the exception, and the caller must not throw it again.</returns>
        public bool Handle(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            int count;

            lock (m_Lock)
            {
                count = ++m_Exceptions;
            }

            if (count == 1)
            {
                Log(exception, @"An exception that nothing handled. The application ends.");
                _ = RunAsync(exception);
                return true;
            }

            Log(exception, $@"Exception {count} that nothing handled, while the application ends after the first one.");

            if (count >= MostExceptions)
            {
                End();
            }

            return true;
        }

        private async Task RunAsync(Exception exception)
        {
            try
            {
                string copy = await KeepCopyAsync();
                string message = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.ProjectPlan.Messages.Message_UnhandledError,
                    copy,
                    exception.Message);

                try
                {
                    await m_ShowErrorAsync(message);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Log(ex, @"The message about the exception could not be shown.");
                }
            }
            finally
            {
                End();
                m_Completion.TrySetResult();
            }
        }

        // The sentence of the message about the copy. A project with no changes needs no copy.
        private async Task<string> KeepCopyAsync()
        {
            try
            {
                if (!m_HasChanges())
                {
                    return Resource.ProjectPlan.Messages.Message_UnhandledErrorNoChanges;
                }

                Task<RecoveryCopyModel?> write = m_WriteCopyAsync();

                if (await Task.WhenAny(write, Task.Delay(m_CopyTimeLimit)) == write
                    && await write is not null)
                {
                    return Resource.ProjectPlan.Messages.Message_UnhandledErrorCopyKept;
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log(ex, @"The recovery copy after the exception failed.");
            }

            return Resource.ProjectPlan.Messages.Message_UnhandledErrorNoCopy;
        }

        private void End()
        {
            lock (m_Lock)
            {
                if (m_HasEnded)
                {
                    return;
                }

                m_HasEnded = true;
            }

            m_End(c_ExitCode);
        }

        // The log is the last thing that may fail here.
        private void Log(Exception exception, string text)
        {
            try
            {
                m_Log(exception, text);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }
    }
}
