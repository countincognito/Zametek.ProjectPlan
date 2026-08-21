using Avalonia.Controls;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Base;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ProjectPlan.Browser
{
    /// <summary>
    /// Message boxes shown as popups over the application root, because a browser has no windows to
    /// parent a dialog to.
    /// </summary>
    /// <remarks>
    /// The file dialogs are deliberately unimplemented. They are declared to return a local file
    /// system path, and a browser has none to return - it hands back an opaque handle that must be
    /// streamed. Making them work is the stream-based file layer, and until that lands these throw
    /// rather than return null, because null here means "the user cancelled" and quietly reporting a
    /// cancellation the user never made would hide the gap instead of showing it.
    /// </remarks>
    public class BrowserDialogService
        : IDialogService
    {
        #region Fields

        private ContentControl? m_Parent;

        #endregion

        #region Private Methods

        private async Task<ButtonResult> ShowMessageBoxAsync(MessageBoxStandardParams standardParams)
        {
            return await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IMsBox<ButtonResult> msg = MessageBoxManager.GetMessageBoxStandard(standardParams);

                // Without a root to hang the popup on there is nowhere to show it, so fall back to
                // the standalone form rather than throwing over a diagnostic.
                return m_Parent is null
                    ? msg.ShowAsync()
                    : msg.ShowAsPopupAsync(m_Parent);
            });
        }

        private static Task<string?> UnsupportedFileDialogAsync(string operation) =>
            throw new NotSupportedException(
                $@"{operation} is not available in the browser: the dialog contract returns a file system path, which a browser cannot provide. This needs the stream-based file layer.");

        #endregion

        #region IDialogService Members

        public object Parent { set => m_Parent = (ContentControl)value; }

        public async Task ShowNotificationAsync(
            string title,
            string header,
            string message)
        {
            await ShowMessageBoxAsync(new MessageBoxStandardParams
            {
                SizeToContent = SizeToContent.WidthAndHeight,
                ContentTitle = title,
                ContentHeader = header,
                ContentMessage = message,
            });
        }

        public async Task ShowErrorAsync(
            string title,
            string header,
            string message)
        {
            await ShowMessageBoxAsync(new MessageBoxStandardParams
            {
                SizeToContent = SizeToContent.WidthAndHeight,
                ContentTitle = title,
                ContentHeader = header,
                ContentMessage = message,
                Icon = Icon.Error,
            });
        }

        public async Task ShowWarningAsync(
            string title,
            string header,
            string message)
        {
            await ShowMessageBoxAsync(new MessageBoxStandardParams
            {
                SizeToContent = SizeToContent.WidthAndHeight,
                ContentTitle = title,
                ContentHeader = header,
                ContentMessage = message,
                Icon = Icon.Warning,
            });
        }

        public async Task ShowInfoAsync(
            string title,
            string header,
            string message,
            bool showMainPageLink = false)
        {
            await ShowMessageBoxAsync(new MessageBoxStandardParams
            {
                SizeToContent = SizeToContent.WidthAndHeight,
                ContentTitle = title,
                ContentHeader = header,
                ContentMessage = message,
                Icon = Icon.Info,
            });
        }

        public async Task ShowInfoAsync(
            string title,
            string header,
            string message,
            double height,
            double width,
            bool showMainPageLink = false)
        {
            await ShowMessageBoxAsync(new MessageBoxStandardParams
            {
                SizeToContent = SizeToContent.Manual,
                ContentTitle = title,
                ContentHeader = header,
                ContentMessage = message,
                Height = height,
                Width = width,
                Icon = Icon.Info,
            });
        }

        public async Task<bool> ShowContextAsync(
            string title,
            string header,
            string message,
            object context)
        {
            ButtonResult result = await ShowMessageBoxAsync(new MessageBoxStandardParams
            {
                SizeToContent = SizeToContent.WidthAndHeight,
                ContentTitle = title,
                ContentHeader = header,
                ContentMessage = message,
                Context = context,
                ButtonDefinitions = ButtonEnum.OkCancel,
                Icon = Icon.None,
            });
            return result == ButtonResult.Ok;
        }

        public async Task<bool> ShowContextAsync(
            string title,
            string header,
            string message,
            object context,
            double height,
            double width)
        {
            ButtonResult result = await ShowMessageBoxAsync(new MessageBoxStandardParams
            {
                SizeToContent = SizeToContent.Manual,
                ContentTitle = title,
                ContentHeader = header,
                ContentMessage = message,
                Context = context,
                Height = height,
                Width = width,
                ButtonDefinitions = ButtonEnum.OkCancel,
                Icon = Icon.None,
            });
            return result == ButtonResult.Ok;
        }

        public async Task<bool> ShowConfirmationAsync(
            string title,
            string header,
            string message)
        {
            ButtonResult result = await ShowMessageBoxAsync(new MessageBoxStandardParams
            {
                SizeToContent = SizeToContent.WidthAndHeight,
                ContentTitle = title,
                ContentHeader = header,
                ContentMessage = message,
                ButtonDefinitions = ButtonEnum.OkCancel,
                Icon = Icon.Info,
            });
            return result == ButtonResult.Ok;
        }

        public Task<string?> ShowOpenFileDialogAsync(
            string initialDirectory,
            IList<IFileFilter> fileFilters) =>
            UnsupportedFileDialogAsync(@"Opening a file");

        public Task<string?> ShowSaveFileDialogAsync(
            string initialFilename,
            string initialDirectory,
            IList<IFileFilter> fileFilters) =>
            UnsupportedFileDialogAsync(@"Saving a file");

        #endregion
    }
}
