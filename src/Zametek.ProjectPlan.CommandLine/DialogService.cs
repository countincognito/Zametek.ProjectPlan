using Zametek.Contract.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    public class DialogService
        : IDialogService
    {
        #region Fields

        private readonly TextWriter m_TextWriter;

        #endregion

        public DialogService(TextWriter textWriter)
        {
            ArgumentNullException.ThrowIfNull(textWriter);
            m_TextWriter = textWriter;
        }


        #region IDialogService Members

        public object Parent { set => throw new InvalidOperationException(); }

        public Task ShowNotificationAsync(
            string title,
            string message,
            bool markdown = false)
        {
            throw new InvalidOperationException();
        }

        public async Task ShowErrorAsync(
            string title,
            string message,
            bool markdown = false)
        {
            await m_TextWriter.WriteLineAsync($@"{title}: {message}");
        }

        public Task ShowWarningAsync(
            string title,
            string message,
            bool markdown = false)
        {
            throw new InvalidOperationException();
        }

        public Task ShowInfoAsync(
            string title,
            string message,
            bool markdown = false)
        {
            throw new InvalidOperationException();
        }

        public Task ShowInfoAsync(
            string title,
            string message,
            double height,
            double width,
            bool markdown = false)
        {
            throw new InvalidOperationException();
        }

        public Task<bool> ShowConfirmationAsync(
            string title,
            string message,
            bool markdown = false)
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
