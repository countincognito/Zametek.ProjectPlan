namespace Zametek.Contract.ProjectPlan
{
    public interface IDialogService
    {
        object Parent { set; }

        Task ShowNotificationAsync(string title, string header, string message);

        Task ShowErrorAsync(string title, string header, string message);

        Task ShowWarningAsync(string title, string header, string message);

        Task ShowInfoAsync(string title, string header, string message, bool showMainPageLink = false);

        Task ShowInfoAsync(string title, string header, string message, double height, double width, bool showMainPageLink = false);

        Task<bool> ShowContextAsync(string title, string header, string message, object context);

        Task<bool> ShowContextAsync(string title, string header, string message, object context, double height, double width);

        Task<bool> ShowConfirmationAsync(string title, string header, string message);

        Task<string?> ShowOpenFileDialogAsync(string initialDirectory, IList<IFileFilter> fileFilters);

        Task<string?> ShowSaveFileDialogAsync(string initialFilename, string initialDirectory, IList<IFileFilter> fileFilters);
    }
}
