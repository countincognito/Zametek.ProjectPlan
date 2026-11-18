using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Serilog;
using Splat;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Zametek.Contract.ProjectPlan;
using Zametek.Shell.ProjectPlan;
using Zametek.View.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

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

                        // The recovery copy of the open project. A run that ends here ends well: the user saved, or
                        // agreed to lose the changes, and so the copy goes.
                        IProjectRecovery projectRecovery = GetRequiredService<IProjectRecovery>();

                        desktopLifetime.Exit += (a, b) =>
                        {
                            projectRecovery.End(keepCopy: false);
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

                        // A handler of Closing cannot wait for the answer to a question. So the guard cancels each request
                        // to close until it knows that the window can close, and then it closes the window itself. It
                        // waits for a save or an export in progress, and it asks only when the project has changes. A
                        // second request while the question is open is cancelled too.
                        var closeGuard = new WindowCloseGuard(
                            isWriting: () => mainViewModel.IsSaving || mainViewModel.IsSavingAs || mainViewModel.IsExporting,
                            writingSource: (INotifyPropertyChanged)mainViewModel,
                            hasChanges: () => mainViewModel.ProjectHasChanges,
                            confirmCloseAsync: () => dialogService.ShowConfirmationAsync(
                                Resource.ProjectPlan.Titles.Title_ProjectUnsavedChanges,
                                string.Empty,
                                Resource.ProjectPlan.Messages.Message_ProjectUnsavedChanges),
                            close: () =>
                            {
                                mainViewModel.CloseLayout();
                                mainWindow.Close();
                            },
                            reportFailure: ex => Log.Error(ex, "The check before the close of the main window failed"));

                        mainWindow.Closing += (_, args) =>
                        {
                            if (closeGuard.ShouldCancel())
                            {
                                args.Cancel = true;
                            }
                        };

                        desktopLifetime.MainWindow = mainWindow;

                        mainWindow.Show();

                        projectRecovery.Start();

                        if (input is not null)
                        {
                            await mainViewModel.OpenProjectFileAsync(input);
                        }

                        splashView.Close();

                        // A settings file that could not be read: the application runs with the default settings, and it
                        // says so. The service moved the file out of the way, and so the next start does not say it again.
                        if (settingService is SettingServiceBase { SettingsReadFailure: not null } settings)
                        {
                            await dialogService.ShowWarningAsync(
                                Resource.ProjectPlan.Titles.Title_ProjectPlan,
                                string.Empty,
                                string.Format(
                                    CultureInfo.CurrentCulture,
                                    Resource.ProjectPlan.Messages.Message_UserSettingsNotReadable,
                                    settings.UnreadableSettingsFilename ?? settings.SettingsFilename));
                        }
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
