using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;

namespace Zametek.Graphs.Avalonia.TestApp.Graphs
{
    // Minimal but genuinely functional implementations of the two UI services an IGraphHost has to
    // provide: a save-file picker and an error reporter. Both resolve the app's single main window to act
    // as the dialog owner / storage-provider host, and marshal onto the UI thread defensively. A real
    // application would route these through its own dialog service instead - this is just enough to make
    // the demo's "Save As..." and error paths work end to end.
    internal static class DemoDialogs
    {
        // The formats Zametek.Graphs.Avalonia's exporter understands (it switches on the file extension).
        private static readonly FilePickerFileType[] s_SaveFileTypes =
        [
            new(@"PNG image") { Patterns = [@"*.png"] },
            new(@"JPEG image") { Patterns = [@"*.jpeg"] },
            new(@"PDF document") { Patterns = [@"*.pdf"] },
            new(@"SVG image") { Patterns = [@"*.svg"] },
            new(@"GraphML") { Patterns = [@"*.graphml"] },
            new(@"GraphViz DOT") { Patterns = [@"*.dot"] },
        ];

        // Prompt for a save path via the platform file picker. Returns the local path, or null if there is
        // no window yet or the user cancelled. Callers are on the UI thread already; the guard just keeps
        // this correct if ever called from elsewhere.
        public static async Task<string?> PickSaveFileAsync(string suggestedFileName)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                return await PickSaveFileCoreAsync(suggestedFileName);
            }

            var tcs = new TaskCompletionSource<string?>();
            Dispatcher.UIThread.Post(async () =>
            {
                try { tcs.SetResult(await PickSaveFileCoreAsync(suggestedFileName)); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            return await tcs.Task;
        }

        // Show a modal error dialog (built in code so the demo needs no extra view). Falls back to a
        // non-modal window if there is no owner yet.
        public static async Task ShowErrorAsync(string message)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                await ShowErrorCoreAsync(message);
                return;
            }

            var tcs = new TaskCompletionSource();
            Dispatcher.UIThread.Post(async () =>
            {
                try { await ShowErrorCoreAsync(message); tcs.SetResult(); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            await tcs.Task;
        }

        private static async Task<string?> PickSaveFileCoreAsync(string suggestedFileName)
        {
            IStorageProvider? storage = MainWindow?.StorageProvider;
            if (storage is null)
            {
                return null;
            }

            IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = @"Save graph image",
                SuggestedFileName = suggestedFileName,
                DefaultExtension = @"png",
                FileTypeChoices = s_SaveFileTypes,
                ShowOverwritePrompt = true,
            });

            return file?.TryGetLocalPath();
        }

        private static async Task ShowErrorCoreAsync(string message)
        {
            Window? owner = MainWindow;

            var okButton = new Button
            {
                Content = @"OK",
                MinWidth = 80,
                HorizontalAlignment = HorizontalAlignment.Right,
                IsDefault = true,
            };

            var dialog = new Window
            {
                Title = @"Error",
                SizeToContent = SizeToContent.WidthAndHeight,
                CanResize = false,
                WindowStartupLocation = owner is null
                    ? WindowStartupLocation.CenterScreen
                    : WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 16,
                    MinWidth = 280,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = message,
                            MaxWidth = 440,
                            TextWrapping = TextWrapping.Wrap,
                        },
                        okButton,
                    },
                },
            };

            okButton.Click += (_, _) => dialog.Close();

            if (owner is not null)
            {
                await dialog.ShowDialog(owner);
            }
            else
            {
                dialog.Show();
            }
        }

        private static Window? MainWindow =>
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    }
}
