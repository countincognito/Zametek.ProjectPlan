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

        /// <summary>
        /// Builds a FilePickerFileType from a FileFilter, ensuring AppleUniformTypeIdentifiers
        /// are set so macOS doesn't grey out files with unregistered extensions (e.g. .zpp).
        /// </summary>
        private static FilePickerFileType BuildFilePickerFileType(FileFilter filter)
        {
            // Extract bare extensions from patterns like "*.zpp" → "zpp"
            var extensions = filter.Patterns
                .Where(p => p.StartsWith("*.") && p.Length > 2)
                .Select(p => p[2..])
                .ToList();

            // On macOS, Avalonia maps AppleUniformTypeIdentifiers to NSOpenPanel allowedContentTypes.
            // Without this, files with unrecognised extensions are greyed out.
            // "public.data" is a catch-all UTI that allows any binary data file to be selected.
            // "public.item" is even broader (includes directories), so we prefer public.data.
            string[] appleUtis = extensions.Count > 0
                ? ["public.data"]
                : ["public.item"];

            return new FilePickerFileType(filter.Name)
            {
                Patterns = filter.Patterns,
                AppleUniformTypeIdentifiers = appleUtis,
                MimeTypes = ["application/octet-stream"]
            };
        }

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
            bool markdown = false,
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
                Markdown = markdown
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
            bool markdown = false,
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
                Markdown = markdown
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
            object context,
            bool markdown = false)
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
                     Markdown = markdown
                 });
            return result == ButtonResult.Ok;
        }

        public async Task<bool> ShowContextAsync(
            string title,
            string header,
            string message,
            object context,
            double height,
            double width,
            bool markdown = false)
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
                    Markdown = markdown
                });
            return result == ButtonResult.Ok;
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

            List<FilePickerFileType> filters = [.. fileFilters.Cast<FileFilter>().Select(BuildFilePickerFileType)];

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

            List<FilePickerFileType> filters = [.. fileFilters.Cast<FileFilter>().Select(BuildFilePickerFileType)];

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
