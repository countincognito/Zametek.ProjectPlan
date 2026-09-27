using Zametek.Contract.ProjectPlan;

namespace Zametek.Engine.ProjectPlan
{
    // What the view models would show in a dialog, a job reports to its sink as it happens: the job's host decides
    // where each message goes. Anything that would need an answer from a user has no place in a job, so it throws.
    internal class JobDialogService
        : IDialogService
    {
        #region Fields

        private IJobSink? m_Sink;
        private int m_ErrorCount;

        #endregion

        #region Properties

        // The sink of the job this service belongs to. JobRunner sets it before it resolves anything else from the
        // job's scope, so every message the job raises reaches it.
        public IJobSink Sink
        {
            get => m_Sink ?? throw new InvalidOperationException();
            set => m_Sink = value;
        }

        // Whether an error has been reported during the job. JobRunner reports a failed chart or graph here rather
        // than let it escape - the desktop shows one in a dialog and carries on - so this is the only trace such a
        // failure leaves, and JobRunner checks it to decide how the job ended.
        public bool HasShownErrors => Volatile.Read(ref m_ErrorCount) > 0;

        #endregion

        #region Private Members

        private Task ReportAsync(
            JobMessageKind kind,
            string title,
            string header,
            string message)
        {
            return Sink.ReportAsync(new JobMessage(kind, title, header, message));
        }

        #endregion

        #region IDialogService Members

        public object Parent { set => throw new InvalidOperationException(); }

        public async Task ShowNotificationAsync(
            string title,
            string header,
            string message)
        {
            await ReportAsync(JobMessageKind.Notification, title, header, message);
        }

        public async Task ShowErrorAsync(
            string title,
            string header,
            string message)
        {
            Interlocked.Increment(ref m_ErrorCount);
            await ReportAsync(JobMessageKind.Error, title, header, message);
        }

        public async Task ShowWarningAsync(
            string title,
            string header,
            string message)
        {
            await ReportAsync(JobMessageKind.Warning, title, header, message);
        }

        public async Task ShowInfoAsync(
            string title,
            string header,
            string message,
            bool showMainPageLink = false)
        {
            await ReportAsync(JobMessageKind.Information, title, header, message);
        }

        public async Task ShowInfoAsync(
            string title,
            string header,
            string message,
            double height,
            double width,
            bool showMainPageLink = false)
        {
            await ReportAsync(JobMessageKind.Information, title, header, message);
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
