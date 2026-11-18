using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Serilog;
using Splat;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Zametek.Common.ProjectPlan;
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

        // The handler of an exception that nothing handled. It is there from the moment that the main window is, with
        // the project and the dialogs that it needs. Before that, an exception ends the start (see the catch below).
        private static UnhandledErrorHandler? s_ErrorHandler;

        // Keeps a recovery copy of a project with changes, when the process ends on an exception of a thread that is not
        // the UI thread. Nothing can stop that end, and no message can show. The next start offers the copy.
        private static Action? s_KeepCopyBeforeTheEnd;

        /// <summary>
        /// Takes the exceptions that leave a command or a subscription of ReactiveUI. ReactiveUI gives them on the thread
        /// that met them, and the handler shows a message, so they go to the UI thread first. With no handler yet, the
        /// exception is thrown on the UI thread, as ReactiveUI does it by default.
        /// </summary>
        public static IObserver<Exception> UnhandledErrors { get; } = new UnhandledErrorObserver();

        private sealed class UnhandledErrorObserver
            : IObserver<Exception>
        {
            public void OnNext(Exception value) =>
                Dispatcher.UIThread.Post(() =>
                {
                    if (s_ErrorHandler is UnhandledErrorHandler handler)
                    {
                        handler.Handle(value);
                    }
                    else
                    {
                        ExceptionDispatchInfo.Capture(value).Throw();
                    }
                });

            public void OnError(Exception error) => OnNext(error);

            public void OnCompleted()
            {
            }
        }

        // Records anything that escapes to the top of a thread or is never observed on a
        // task. These handlers only observe - the runtime still terminates the process
        // exactly as it would otherwise (the first one keeps a recovery copy before it
        // does) - but without them a crash leaves nothing behind
        // except a stderr message no user ever sees. A render-thread exception, for
        // instance, is unreachable by any try/catch we could write, so the log file is the
        // only place its stack trace can survive.
        private static void RegisterGlobalExceptionHandlers()
        {
            // A save that could not put its temporary file in the place of the file, and wrote the file in place.
            FileStreamHelper.WroteInPlace += (filename, reason) =>
                Log.Warning(reason, "The file {File} was written in place, and not by way of a temporary file", filename);

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                Log.Fatal(args.ExceptionObject as Exception, "Unhandled exception (terminating: {IsTerminating})", args.IsTerminating);

                if (args.IsTerminating)
                {
                    try
                    {
                        s_KeepCopyBeforeTheEnd?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "The recovery copy before the end of the process failed");
                    }
                }

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

                        // The recovery copy of the open project. A run that ends well removes it: the user saved, or
                        // agreed to lose the changes. A run that ends after an exception keeps it for the next run.
                        IProjectRecovery projectRecovery = GetRequiredService<IProjectRecovery>();

                        desktopLifetime.Exit += (a, b) =>
                        {
                            projectRecovery.End(keepCopy: s_ErrorHandler?.IsEnding == true);
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

                        // An exception that nothing handled: the application does not go on, because nobody knows what
                        // state it is in. It keeps a recovery copy of a project with changes, tells the user, and ends.
                        // From here on, the UI thread gives its exceptions to the handler and stays alive for the
                        // message.
                        var errorHandler = new UnhandledErrorHandler(
                            hasChanges: () => mainViewModel.ProjectHasChanges,
                            writeCopyAsync: () => projectRecovery.WriteCopyAsync(RecoveryCopyReasons.Error),
                            showErrorAsync: message => dialogService.ShowErrorAsync(Resource.ProjectPlan.Titles.Title_Error, string.Empty, message),
                            end: desktopLifetime.Shutdown,
                            log: (ex, text) => Log.Fatal(ex, "{Text}", text));
                        s_ErrorHandler = errorHandler;

                        // The wait has a time limit: the thread that ends the process can hold what the copy needs.
                        s_KeepCopyBeforeTheEnd = () =>
                        {
                            if (!errorHandler.IsEnding
                                && mainViewModel.ProjectHasChanges)
                            {
                                projectRecovery.WriteCopyAsync(RecoveryCopyReasons.Error).Wait(UnhandledErrorHandler.CopyTimeLimit);
                            }
                        };

                        Dispatcher.UIThread.UnhandledException += (_, args) =>
                        {
                            args.Handled = errorHandler.Handle(args.Exception);
                        };

                        mainWindow.Closing += (_, args) =>
                        {
                            // The end after an exception asks nothing: the recovery copy has the changes.
                            if (!errorHandler.IsEnding
                                && closeGuard.ShouldCancel())
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

                        // A copy that an earlier run left, because it ended before a save: the user says what happens
                        // to it. The main window is on the screen by now, and the question is a dialog of that window.
                        await projectRecovery.OfferLeftCopiesAsync();
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
