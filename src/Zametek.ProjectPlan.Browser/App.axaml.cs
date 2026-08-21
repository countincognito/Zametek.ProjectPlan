using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Serilog;
using Serilog.Events;
using Splat;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Zametek.Contract.ProjectPlan;
using Zametek.ProjectPlan.Core;

namespace Zametek.ProjectPlan.Browser
{
    public partial class App
        : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private static T GetRequiredService<T>() =>
            Locator.Current.GetService<T>() ?? throw new NullReferenceException($"{Resource.ProjectPlan.Messages.Message_UnableToResolveType} {typeof(T).FullName}");

        // Writes to the browser's developer console, which is the only sink a WebAssembly build has:
        // the desktop head's rolling file is not an option without a file system. Anything at Error or
        // above goes to stderr so that it arrives as console.error rather than console.log, which is
        // what makes a failure stand out in the developer tools instead of scrolling past in the noise.
        private static Serilog.ILogger ConfigureSerilog() =>
            new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Error)
                .CreateLogger();

        // Records anything that escapes to the top of a task or thread. Without these a failure inside
        // the reactive pipeline leaves nothing behind at all - there is no window to raise a dialog
        // over and no log file to inspect afterwards.
        private static void RegisterGlobalExceptionHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                Log.Fatal(args.ExceptionObject as Exception, "Unhandled exception (terminating: {IsTerminating})", args.IsTerminating);
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                Log.Error(args.Exception, "Unobserved task exception");
            };
        }

        public override void OnFrameworkInitializationCompleted()
        {
            Log.Logger = ConfigureSerilog();
            RegisterGlobalExceptionHandlers();
            Log.Information(
                "Application starting up (version {Version}, {FrameworkDescription}, {OSDescription})",
                Resource.ProjectPlan.Labels.Label_AppVersion,
                RuntimeInformation.FrameworkDescription,
                RuntimeInformation.OSDescription);

            // The browser has no windows, so the single-view lifetime is the only one available:
            // whatever is assigned to MainView becomes the whole application surface.
            if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime)
            {
                singleViewLifetime.MainView = BuildRootView();
            }

            base.OnFrameworkInitializationCompleted();
        }

        // The application shell is not wired up yet: MainView is a Window, which a browser cannot
        // host, so splitting it into a UserControl shell plus a thin desktop window is still to come.
        // Until then this builds the full view-model graph and reports the outcome, which is the
        // thing actually worth knowing - it exercises every registration in the shared composition
        // root, the whole view-model layer, and the initial compile, on the browser's single thread.
        private static Control BuildRootView()
        {
            try
            {
                CompositionRoot.Build();

                IMainViewModel mainViewModel = GetRequiredService<IMainViewModel>();

                Log.Information(
                    "View model graph resolved and compiled (project title {ProjectTitle})",
                    mainViewModel.ProjectTitle);

                return new TextBlock
                {
                    Text = $"Project Plan{Environment.NewLine}The view model graph resolved and compiled. Project title: '{mainViewModel.ProjectTitle}'.",
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
            }
            catch (Exception ex)
            {
                // Painted onto the page as well as logged: a startup failure that only reaches the
                // developer console is, to anyone who does not have it open, a blank page.
                Log.Fatal(ex, "Fatal startup error");

                return new TextBlock
                {
                    Text = $"Project Plan failed to start.{Environment.NewLine}{ex}",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(16),
                };
            }
        }
    }
}
