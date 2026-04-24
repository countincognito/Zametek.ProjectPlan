using Avalonia;
using Avalonia.Controls;
using ReactiveUI.Avalonia;
using System;
using System.Threading.Tasks;

namespace Zametek.ProjectPlan
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static async Task Main(string[] args)
        {










            //await Task.Run(() =>
            //{
            //    Bootstrapper.RegisterIOC();
            //});



            Bootstrapper.RegisterIOC();



            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args, ShutdownMode.OnMainWindowClose);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                //.UseReactiveUI()
                .UsePlatformDetect()
                .LogToTrace();
    }
}
