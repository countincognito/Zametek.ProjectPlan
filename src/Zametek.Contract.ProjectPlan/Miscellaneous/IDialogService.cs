namespace Zametek.Contract.ProjectPlan
{
    public interface IDialogService
    {
        object Parent { set; }

        Task ShowNotificationAsync(string title, string header, string message, bool markdown = false);

        Task ShowErrorAsync(string title, string header, string message, bool markdown = false);

        Task ShowWarningAsync(string title, string header, string message, bool markdown = false);

        Task ShowInfoAsync(string title, string header, string message, bool markdown = false, bool showMainPageLink = false);

        Task ShowInfoAsync(string title, string header, string message, double height, double width, bool markdown = false, bool showMainPageLink = false);

        Task<bool> ShowContextAsync(string title, string header, string message, object context, bool markdown = false);

        Task<bool> ShowContextAsync(string title, string header, string message, object context, double height, double width, bool markdown = false);

        Task<bool> ShowConfirmationAsync(string title, string header, string message, bool markdown = false);

        Task<string?> ShowOpenFileDialogAsync(string initialDirectory, IList<IFileFilter> fileFilters);

        Task<string?> ShowSaveFileDialogAsync(string initialFilename, string initialDirectory, IList<IFileFilter> fileFilters);
    }
}
