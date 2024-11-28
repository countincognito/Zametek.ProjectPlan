using AutoMapper;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MsBox.Avalonia.Base;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class DialogService
        : IDialogService
    {
        #region Fields

        private Window? m_Parent;
        private readonly IMapper m_Mapper;

        #endregion

        public DialogService(IMapper mapper)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            m_Mapper = mapper;
        }

        #region Private Methods

        private async Task<ButtonResult> ShowMessageBoxAsync(MessageBoxStandardParams standardParams)
        {
            standardParams.WindowIcon = m_Parent!.Icon ?? throw new ArgumentNullException(Resource.ProjectPlan.Messages.Message_NoWindowIconAvailable);
            IMsBox<ButtonResult>? msg = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(standardParams);
            return await msg.ShowWindowDialogAsync(m_Parent);
        }

        #endregion

        #region IDialogService Members

        public object Parent { set => m_Parent = (Window)value; }

        public async Task ShowNotificationAsync(
            string title,
            string header,
            string message,
            bool markdown = false)
        {
            await ShowMessageBoxAsync(new MessageBoxStandardParams
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ContentTitle = title,
                ContentHeader = header,
                ContentMessage = message,
                Markdown = markdown
            });
        }

        public async Task ShowErrorAsync(
            string title,
            string header,
            string message,
            bool markdown = false)
        {
            await ShowMessageBoxAsync(new MessageBoxStandardParams
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ContentTitle = title,
                ContentHeader = header,
                ContentMessage = message,
                Icon = Icon.Error,
                Markdown = markdown
            });
        }

        public async Task ShowWarningAsync(
            string title,
            string header,
            string message,
            bool markdown = false)
        {
            await ShowMessageBoxAsync(new MessageBoxStandardParams
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ContentTitle = title,
                ContentHeader = header,
                ContentMessage = message,
                Icon = Icon.Warning,
                Markdown = markdown
            });
        }

        public async Task ShowInfoAsync(
            string title,
            string header,
            string message,
            bool markdown = false)
        {
            await ShowMessageBoxAsync(new MessageBoxStandardParams
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ContentTitle = title,
                ContentHeader = header,
                ContentMessage = message,
                Icon = Icon.Info,
                Markdown = markdown
            });
        }

        public async Task ShowInfoAsync(
            string title,
            string header,
            string message,
            double height,
            double width,
            bool markdown = false)
        {
            await ShowMessageBoxAsync(new MessageBoxStandardParams
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.Manual,
                ContentTitle = title,
                ContentHeader = header,
                ContentMessage = message,
                Height = height,
                Width = width,
                Icon = Icon.Info,
                Markdown = markdown
            });
        }

        public async Task<bool> ShowConfirmationAsync(
            string title,
            string header,
            string message,
            bool markdown = false)
        {
            ButtonResult result = await ShowMessageBoxAsync(new MessageBoxStandardParams
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ContentTitle = title,
                ContentHeader = header,
                ContentMessage = message,
                ButtonDefinitions = ButtonEnum.YesNo,
                Icon = Icon.Info,
                Markdown = markdown
            });
            return result == ButtonResult.Yes;
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

            var filters = m_Mapper.Map<IList<IFileFilter>, List<FilePickerFileType>>(fileFilters);

            var options = new FilePickerOpenOptions
            {
                AllowMultiple = false,
                SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(initialDirectory),
                FileTypeFilter = filters
            };

            IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(options);

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

            var filters = m_Mapper.Map<IList<IFileFilter>, List<FilePickerFileType>>(fileFilters);

            var options = new FilePickerSaveOptions
            {
                SuggestedFileName = initialFilename,
                SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(initialDirectory),
                FileTypeChoices = filters
            };

            IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(options);

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
