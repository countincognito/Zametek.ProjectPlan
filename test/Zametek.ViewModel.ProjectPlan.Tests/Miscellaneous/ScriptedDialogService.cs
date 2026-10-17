using System.Collections.Generic;
using System.Threading.Tasks;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A dialog service that remembers what it was asked to show, and answers as it was told to: what the user would choose in the
    /// dialogs that open a file, save a file and ask a question.
    /// </summary>
    internal sealed class ScriptedDialogService
        : IDialogService
    {
        public List<(string Title, string Header, string Message)> Errors { get; } = [];

        public List<string> Questions { get; } = [];

        public List<(string Name, string Directory, IList<IFileFilter> Filters)> SaveDialogs { get; } = [];

        /// <summary>The answer to a question.</summary>
        public bool WantsReport { get; set; } = true;

        /// <summary>The file that the dialog that saves a file gives, or null when the user cancels it.</summary>
        public string? SaveFilename { get; set; }

        /// <summary>The file that the dialog that opens a file gives, or null when the user cancels it.</summary>
        public string? OpenFilename { get; set; }

        public object Parent { set { } }

        public Task ShowNotificationAsync(string title, string header, string message) => Task.CompletedTask;

        public Task ShowErrorAsync(string title, string header, string message)
        {
            Errors.Add((title, header, message));
            return Task.CompletedTask;
        }

        public Task ShowWarningAsync(string title, string header, string message) => Task.CompletedTask;

        public Task ShowInfoAsync(string title, string header, string message, bool showMainPageLink = false) => Task.CompletedTask;

        public Task ShowInfoAsync(string title, string header, string message, double height, double width, bool showMainPageLink = false) => Task.CompletedTask;

        public Task<bool> ShowContextAsync(string title, string header, string message, object context) => Task.FromResult(false);

        public Task<bool> ShowContextAsync(string title, string header, string message, object context, double height, double width) => Task.FromResult(false);

        public Task<bool> ShowConfirmationAsync(string title, string header, string message)
        {
            Questions.Add(message);
            return Task.FromResult(WantsReport);
        }

        public Task<string?> ShowOpenFileDialogAsync(string initialDirectory, IList<IFileFilter> fileFilters) => Task.FromResult(OpenFilename);

        public Task<string?> ShowSaveFileDialogAsync(string initialFilename, string initialDirectory, IList<IFileFilter> fileFilters)
        {
            SaveDialogs.Add((initialFilename, initialDirectory, fileFilters));
            return Task.FromResult(SaveFilename);
        }
    }
}
