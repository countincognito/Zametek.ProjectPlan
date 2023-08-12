using AutoMapper;
using Avalonia.Controls;
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
            standardParams.WindowIcon = m_Parent!.Icon;
            IMsBox<ButtonResult>? msg = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(standardParams);
            return await msg.ShowWindowDialogAsync(m_Parent);
        }

        #endregion

        #region IDialogService Members

        public object Parent { set => m_Parent = (Window)value; }

        public async Task ShowNotificationAsync(
            string title,
            string message,
            bool markdown = false)
        {
            await ShowMessageBoxAsync(new MessageBoxStandardParams
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ContentTitle = title,
                ContentMessage = message,
                Markdown = markdown
            });
        }

        public async Task ShowErrorAsync(
            string title,
            string message,
            bool markdown = false)
        {
            await ShowMessageBoxAsync(new MessageBoxStandardParams
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ContentTitle = title,
                ContentMessage = message,
                Icon = Icon.Error,
                Markdown = markdown
            });
        }

        public async Task ShowWarningAsync(
            string title,
            string message,
            bool markdown = false)
        {
            await ShowMessageBoxAsync(new MessageBoxStandardParams
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ContentTitle = title,
                ContentMessage = message,
                Icon = Icon.Warning,
                Markdown = markdown
            });
        }

        public async Task ShowInfoAsync(
            string title,
            string message,
            bool markdown = false)
        {
            await ShowMessageBoxAsync(new MessageBoxStandardParams
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ContentTitle = title,
                ContentMessage = message,
                Icon = Icon.Info,
                Markdown = markdown
            });
        }

        public async Task ShowInfoAsync(
            string title,
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
                ContentMessage = message,
                Height = height,
                Width = width,
                Icon = Icon.Info,
                Markdown = markdown
            });
        }

        public async Task<bool> ShowConfirmationAsync(
            string title,
            string message,
            bool markdown = false)
        {
            ButtonResult result = await ShowMessageBoxAsync(new MessageBoxStandardParams
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ContentTitle = title,
                ContentMessage = message,
                ButtonDefinitions = ButtonEnum.YesNo,
                Icon = Icon.Info,
                Markdown = markdown
            });
            return result == ButtonResult.Yes;
        }

        public async Task<string?> ShowOpenFileDialogAsync(
            string initialFilename,
            string initialDirectory,
            IList<IFileFilter> fileFilters)
        {
            var dlg = new OpenFileDialog
            {
                AllowMultiple = false,
                InitialFileName = initialFilename,
                Directory = initialDirectory,
                Filters = m_Mapper.Map<IList<IFileFilter>, List<FileDialogFilter>>(fileFilters)
            };

            if (m_Parent is null)
            {
                return null;
            }

            string[]? files = await dlg.ShowAsync(m_Parent);
            return files?.FirstOrDefault();
        }

        public async Task<string?> ShowSaveFileDialogAsync(
            string initialFilename,
            string initialDirectory,
            IList<IFileFilter> fileFilters)
        {
            var dlg = new SaveFileDialog
            {
                InitialFileName = initialFilename,
                Directory = initialDirectory,
                Filters = m_Mapper.Map<IList<IFileFilter>, List<FileDialogFilter>>(fileFilters)
            };

            if (m_Parent is null)
            {
                return null;
            }

            return await dlg.ShowAsync(m_Parent);
        }

        #endregion
    }
}
