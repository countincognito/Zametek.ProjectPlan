using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Base;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Zametek.Contract.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class DialogService
        : IDialogService
    {
        #region Fields

        private Window? m_Parent;
        private readonly ProjectPlanMapper m_Mapper;

        #endregion

        public DialogService(ProjectPlanMapper mapper)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            m_Mapper = mapper;
        }

        #region Private Methods

        private async Task<ButtonResult> ShowMessageBoxAsync(MessageBoxStandardParams standardParams)
        {
            return await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (m_Parent is null)
                {
                    throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_NoWindowIconAvailable);
                }
                standardParams.WindowIcon = m_Parent.Icon ?? throw new ArgumentNullException(Resource.ProjectPlan.Messages.Message_NoWindowIconAvailable);
                IMsBox<ButtonResult>? msg = MessageBoxManager.GetMessageBoxStandard(standardParams);
                return msg.ShowWindowDialogAsync(m_Parent);
            });
        }

        #endregion

        #region IDialogService Members

        public object Parent { set => m_Parent = (Window)value; }

        public async Task ShowNotificationAsync(
            string title,
            string header,
            string message)
        {
            await ShowMessageBoxAsync(new MessageBoxStandardParams
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
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
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
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
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
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
            var @params = new MessageBoxStandardParams
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ContentTitle = title,
                ContentHeader = header,
                ContentMessage = message,
                Icon = Icon.Info,
            };

            if (showMainPageLink)
            {
                @params.HyperLinkParams = new HyperLinkParams
                {
                    Text = UriHelper.LinkMainPage.AbsoluteUri,
                    Action = UriHelper.OpenMainPage,
                };
            }

            await ShowMessageBoxAsync(@params);
        }

        public async Task ShowInfoAsync(
            string title,
            string header,
            string message,
            double height,
            double width,
            bool showMainPageLink = false)
        {
            var @params = new MessageBoxStandardParams
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.Manual,
                ContentTitle = title,
                ContentHeader = header,
                ContentMessage = message,
                Height = height,
                Width = width,
                Icon = Icon.Info,
            };

            if (showMainPageLink)
            {
                @params.HyperLinkParams = new HyperLinkParams
                {
                    Text = UriHelper.LinkMainPage.AbsoluteUri,
                    Action = UriHelper.OpenMainPage,
                };
            }

            await ShowMessageBoxAsync(@params);
        }

        public async Task<bool> ShowContextAsync(
            string title,
            string header,
            string message,
            object context)
        {
            var result = await ShowMessageBoxAsync(
                 new MessageBoxStandardParams
                 {
                     WindowStartupLocation = WindowStartupLocation.CenterOwner,
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
            var result = await ShowMessageBoxAsync(
                new MessageBoxStandardParams
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
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
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ContentTitle = title,
                ContentHeader = header,
                ContentMessage = message,
                ButtonDefinitions = ButtonEnum.OkCancel,
                Icon = Icon.Info,
            });
            return result == ButtonResult.Ok;
        }

        public async Task<string?> ShowOpenFileDialogAsync(
            string initialDirectory,
            IList<IFileFilter> fileFilters)
        {
            var topLevel = TopLevel.GetTopLevel(m_Parent);

            if (topLevel is null)
            {
                return null;
            }

            List<FilePickerFileType> filters = [.. fileFilters.Cast<FileFilter>().Select(m_Mapper.ToFilePickerFileType)];

            var options = new FilePickerOpenOptions
            {
                AllowMultiple = false,
                SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(initialDirectory),
                FileTypeFilter = filters
            };

            IReadOnlyList<IStorageFile> files = await Dispatcher.UIThread.InvokeAsync(() => topLevel.StorageProvider.OpenFilePickerAsync(options));

            Uri? path = files?.FirstOrDefault()?.Path;

            if (path is not null
                && path.IsFile)
            {
                return path.LocalPath;
            }

            return null;
        }

        public async Task<string?> ShowSaveFileDialogAsync(
            string initialFilename,
            string initialDirectory,
            IList<IFileFilter> fileFilters)
        {
            var topLevel = TopLevel.GetTopLevel(m_Parent);

            if (topLevel is null)
            {
                return null;
            }

            List<FilePickerFileType> filters = [.. fileFilters.Cast<FileFilter>().Select(m_Mapper.ToFilePickerFileType)];

            var options = new FilePickerSaveOptions
            {
                SuggestedFileName = initialFilename,
                SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(initialDirectory),
                FileTypeChoices = filters
            };

            IStorageFile? file = await Dispatcher.UIThread.InvokeAsync(() => topLevel.StorageProvider.SaveFilePickerAsync(options));

            Uri? path = file?.Path;

            if (path is not null
                && path.IsFile)
            {
                return path.LocalPath;
            }

            return null;
        }

        #endregion
    }
}
