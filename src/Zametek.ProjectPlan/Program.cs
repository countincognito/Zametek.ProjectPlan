using Avalonia;
using Avalonia.Controls;
using ReactiveUI.Avalonia;
using System;

namespace Zametek.ProjectPlan
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        //
        // If your Main method is marked as public static async Task Main, it can break the STA state
        // even if the [STAThread] attribute is present. Especially when trying to access COM components
        // like the clipboard.
        // https://github.com/AvaloniaUI/Avalonia/issues/20007
        [STAThread]
        public static void Main(string[] args)
        {
            CompositionRoot.Configure();
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args, ShutdownMode.OnMainWindowClose);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
#if DEBUG
                .WithDeveloperTools()
#endif
                .LogToTrace()
                .UseReactiveUI(builder => builder.WithAvalonia());
    }
}
