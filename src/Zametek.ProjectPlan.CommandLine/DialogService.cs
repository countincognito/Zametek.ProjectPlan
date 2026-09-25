using Zametek.Contract.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    public class DialogService
        : IDialogService
    {
        #region Fields

        private int m_ErrorCount;

        #endregion

        public DialogService()
        {
        }

        #region Properties

        // Whether an error has been shown during the run. The chart and graph
        // view models catch a failed export and report it here rather than let
        // it escape - the desktop shows it in a dialog and carries on - so this
        // is the only trace such a failure leaves, and Program checks it to
        // fail the run.
        public bool HasShownErrors => Volatile.Read(ref m_ErrorCount) > 0;

        #endregion

        #region IDialogService Members

        public object Parent { set => throw new InvalidOperationException(); }

        public async Task ShowNotificationAsync(
            string title,
            string header,
            string message)
        {
            await Console.Out.WriteLineAsync($@"{title}: {message}");
        }

        public async Task ShowErrorAsync(
            string title,
            string header,
            string message)
        {
            Interlocked.Increment(ref m_ErrorCount);
            await Console.Error.WriteLineAsync($@"{title}: {message}");
        }

        public async Task ShowWarningAsync(
            string title,
            string header,
            string message)
        {
            await Console.Error.WriteLineAsync($@"{title}: {message}");
        }

        public async Task ShowInfoAsync(
            string title,
            string header,
            string message,
            bool showMainPageLink = false)
        {
            await Console.Out.WriteLineAsync($@"{title}: {message}");
        }

        public async Task ShowInfoAsync(
            string title,
            string header,
            string message,
            double height,
            double width,
            bool showMainPageLink = false)
        {
            await Console.Out.WriteLineAsync($@"{title}: {message}");
        }

        public Task<bool> ShowConfirmationAsync(
            string title,
            string header,
            string message)
        {
            throw new InvalidOperationException();
        }

        public Task<bool> ShowContextAsync(
            string title,
            string header,
            string message,
            object context)
        {
            throw new InvalidOperationException();
        }

        public Task<bool> ShowContextAsync(
            string title,
            string header,
            string message,
            object context,
            double height,
            double width)
        {
            throw new InvalidOperationException();
        }

        public Task<string?> ShowOpenFileDialogAsync(
            string initialDirectory,
            IList<IFileFilter> fileFilters)
        {
            throw new InvalidOperationException();
        }

        public Task<string?> ShowSaveFileDialogAsync(
            string initialFilename,
            string initialDirectory,
            IList<IFileFilter> fileFilters)
        {
            throw new InvalidOperationException();
        }

        #endregion
    }
}
