using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Zametek.ProjectPlan.Desktop
{
    /// <summary>
    /// The message for a start that failed, with its cause. The start can fail before the services of the application are
    /// there, for example when the application cannot make its settings folder, and so the dialogs of the application are not
    /// there either. This window needs nothing of them. Before, such a start wrote only to the log, which it maybe could not
    /// make, and to the standard error stream, which a Windows program does not show: the application ended with no word (from
    /// the review of src, test and pkg).
    /// </summary>
    internal sealed class StartupFailureWindow
        : Window
    {
        public StartupFailureWindow(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            Title = Resource.ProjectPlan.Titles.Title_ProjectPlan;
            Width = 560;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var message = new SelectableTextBlock
            {
                Text = BuildMessage(exception),
                TextWrapping = TextWrapping.Wrap,
            };

            var close = new Button
            {
                Content = Resource.ProjectPlan.Labels.Label_Ok,
                HorizontalAlignment = HorizontalAlignment.Right,
                IsDefault = true,
                IsCancel = true,
                MinWidth = 80,
            };
            close.Click += (_, _) => Close();

            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 16,
                Children = { message, close },
            };
        }

        /// <summary>
        /// The text of the message: that the application cannot start, and the cause, which is the message of the exception.
        /// </summary>
        public static string BuildMessage(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            return string.Format(
                CultureInfo.CurrentCulture,
                Resource.ProjectPlan.Messages.Message_StartupFailed,
                exception.GetBaseException().Message);
        }
    }
}
