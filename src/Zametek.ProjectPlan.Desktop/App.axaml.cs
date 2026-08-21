using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Serilog;
using Splat;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Zametek.Contract.ProjectPlan;
using Zametek.ProjectPlan.Core;
using Zametek.View.ProjectPlan;

namespace Zametek.ProjectPlan.Desktop
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

        private static Serilog.ILogger ConfigureSerilog()
        {
            string productSettingsPath = SettingFileHelper.ProductSettingsFolderLocation();
            string logDir = Path.Combine(productSettingsPath, "logs");
            Directory.CreateDirectory(logDir);
            string logPath = Path.Combine(logDir, "app-.log");
            return new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
                .CreateLogger();
        }

        // Records anything that escapes to the top of a thread or is never observed on a
        // task. These handlers only observe - the runtime still terminates the process
        // exactly as it would otherwise - but without them a crash leaves nothing behind
        // except a stderr message no user ever sees. A render-thread exception, for
        // instance, is unreachable by any try/catch we could write, so the log file is the
        // only place its stack trace can survive.
        private static void RegisterGlobalExceptionHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                Log.Fatal(args.ExceptionObject as Exception, "Unhandled exception (terminating: {IsTerminating})", args.IsTerminating);

                // Flush only when the process is actually going down: CloseAndFlush swaps
                // in a silent logger, which would mute a survivable exception's successors.
                if (args.IsTerminating)
                {
                    Log.CloseAndFlush();
                }
            };

            // Raised when a faulted task is collected with nobody having awaited it. The
            // default (swallow) behaviour is left alone by not calling SetObserved.
            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                Log.Error(args.Exception, "Unobserved task exception");
            };
        }

        public override async void OnFrameworkInitializationCompleted()
        {
            Log.Logger = ConfigureSerilog();
            RegisterGlobalExceptionHandlers();
            Log.Information(
                "Application starting up (version {Version}, {FrameworkDescription}, {OSDescription})",
                Resource.ProjectPlan.Labels.Label_AppVersion,
                RuntimeInformation.FrameworkDescription,
                RuntimeInformation.OSDescription);

            try
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
                {
                    var splashView = new SplashView();
                    var splashViewModel = new SplashViewModel();

                    splashView.DataContext = splashViewModel;

                    desktopLifetime.MainWindow = splashView;

                    splashView.Show();

                    string? input = null;

                    desktopLifetime.Startup += (sender, args) =>
                    {
                        input = args?.Args?.FirstOrDefault();
                    };

                    try
                    {
                        await Task.Factory.StartNew(
                            CompositionRoot.Build,
                            splashViewModel.CancellationToken);

                        ISettingService settingService = GetRequiredService<ISettingService>();
                        string selectedTheme = settingService.SelectedTheme;

                        IMainViewModel mainViewModel = GetRequiredService<IMainViewModel>();

                        DataContext = mainViewModel;

                        desktopLifetime.Exit += (a, b) =>
                        {
                            mainViewModel.CloseLayout();
                            Log.Information("Application shutting down");
                            Log.CloseAndFlush();
                        };

                        MainWindow mainWindow = new()
                        {
                            DataContext = mainViewModel,
                            InitialTheme = selectedTheme
                        };

                        IDialogService dialogService = GetRequiredService<IDialogService>();
                        dialogService.Parent = mainWindow;

                        // Cancelling the window closing does not work when using an async handler,
                        // and trying to force Wait on the return dialog freezes the UI thread.
                        // This solution is the hack below, where CancelClose automatically cancels
                        // the closing event first, then CheckClose checks to see if the project
                        // has updates.
                        // If there are no updates, CheckClose removes all handlers and forces a new close.
                        // If there are updates, then the dialog requests permission to proceed.
                        // If yes, then it continues as before. If no, then CheckClose removes itself
                        // and then adds back all the handlers in the correct order (i.e. the same
                        // initial state) and then immediately returns.
                        void CancelClose(object? sender, CancelEventArgs args)
                        {
                            args.Cancel = true;
                        }

                        async void CheckClose(object? sender, CancelEventArgs args)
                        {
                            mainWindow.Closing -= CancelClose;

                            if (mainViewModel.ProjectHasChanges)
                            {
                                bool wishToClose = await dialogService.ShowConfirmationAsync(
                                    Resource.ProjectPlan.Titles.Title_ProjectUnsavedChanges,
                                    string.Empty,
                                    Resource.ProjectPlan.Messages.Message_ProjectUnsavedChanges);

                                if (!wishToClose)
                                {
                                    // Clearing the rest of the handlers and then adding
                                    // them back in the correct order.
                                    mainWindow.Closing -= CheckClose;
                                    mainWindow.Closing += CancelClose;
                                    mainWindow.Closing += CheckClose;
                                    return;
                                }
                            }

                            mainWindow.Closing -= CheckClose;
                            mainViewModel.CloseLayout();
                            mainWindow.Close();
                        }

                        mainWindow.Closing += CancelClose;
                        mainWindow.Closing += CheckClose;

                        desktopLifetime.MainWindow = mainWindow;

                        mainWindow.Show();

                        if (input is not null)
                        {
                            await mainViewModel.OpenProjectFileAsync(input);
                        }

                        splashView.Close();
                    }
                    catch (TaskCanceledException)
                    {
                        splashView.Close();
                        return;
                    }
                }
                //else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime)
                //{
                //    var mainView = new MainView()
                //    {
                //        DataContext = mainViewModel
                //    };

                //    singleViewLifetime.MainView = mainView;
                //}
                base.OnFrameworkInitializationCompleted();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Fatal startup error");
                Console.Error.WriteLine($"Fatal startup error: {ex}");
                Log.CloseAndFlush();
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown(1);
                }
            }
        }
    }
}
