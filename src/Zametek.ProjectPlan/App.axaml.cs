using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Serilog;
using Splat;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Zametek.Contract.ProjectPlan;
using Zametek.View.ProjectPlan;

namespace Zametek.ProjectPlan
{
    public partial class App
        : Application
    {
        public override void Initialize()
        {
            RxSchedulers.MainThreadScheduler = AvaloniaScheduler.Instance;
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

        public override async void OnFrameworkInitializationCompleted()
        {
            Log.Logger = ConfigureSerilog();
            Log.Information("Application starting up");

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
                            Log.CloseAndFlush();
                        };

                        MainView mainView = new()
                        {
                            DataContext = mainViewModel,
                            InitialTheme = selectedTheme
                        };

                        IDialogService dialogService = GetRequiredService<IDialogService>();
                        dialogService.Parent = mainView;

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
                            mainView.Closing -= CancelClose;

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
                                    mainView.Closing -= CheckClose;
                                    mainView.Closing += CancelClose;
                                    mainView.Closing += CheckClose;
                                    return;
                                }
                            }

                            mainView.Closing -= CheckClose;
                            mainViewModel.CloseLayout();
                            mainView.Close();
                        }

                        mainView.Closing += CancelClose;
                        mainView.Closing += CheckClose;

                        desktopLifetime.MainWindow = mainView;

                        mainView.Show();

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
