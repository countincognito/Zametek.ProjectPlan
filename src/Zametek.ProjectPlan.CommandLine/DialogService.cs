using Zametek.Contract.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    public class DialogService
        : IDialogService
    {
        #region Fields

        #endregion

        public DialogService()
        {
        }

        #region IDialogService Members

        public object Parent { set => throw new InvalidOperationException(); }

        public async Task ShowNotificationAsync(
            string title,
            string header,
            string message,
            bool markdown = false)
        {
            await Console.Out.WriteLineAsync($@"{title}: {message}");
        }

        public async Task ShowErrorAsync(
            string title,
            string header,
            string message,
            bool markdown = false)
        {
            await Console.Error.WriteLineAsync($@"{title}: {message}");
        }

        public async Task ShowWarningAsync(
            string title,
            string header,
            string message,
            bool markdown = false)
        {
            await Console.Error.WriteLineAsync($@"{title}: {message}");
        }

        public async Task ShowInfoAsync(
            string title,
            string header,
            string message,
            bool markdown = false)
        {
            await Console.Out.WriteLineAsync($@"{title}: {message}");
        }

        public async Task ShowInfoAsync(
            string title,
            string header,
            string message,
            double height,
            double width,
            bool markdown = false)
        {
            await Console.Out.WriteLineAsync($@"{title}: {message}");
        }

        public Task<bool> ShowConfirmationAsync(
            string title,
            string header,
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
