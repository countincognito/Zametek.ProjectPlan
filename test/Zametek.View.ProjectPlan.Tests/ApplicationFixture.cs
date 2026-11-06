using Autofac;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using ReactiveUI.Avalonia;
using Xunit;
using Zametek.Contract.ProjectPlan;
using Zametek.ProjectPlan.Browser;
using Zametek.Shell.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan.Tests
{
    /// <summary>
    /// The application as a head builds it, with its styles and its data templates, in a headless window.
    /// </summary>
    public sealed class HeadlessTestApp
        : Application
    {
        public override void Initialize()
        {
            var baseUri = new Uri(@"avares://Zametek.View.ProjectPlan.Tests/");
            DataTemplates.Add(new ViewLocator());
            Styles.Add(new StyleInclude(baseUri) { Source = new Uri(@"avares://Zametek.Shell.ProjectPlan/Themes/AppStyles.axaml") });
            Resources.MergedDictionaries.Add(new ResourceInclude(baseUri) { Source = new Uri(@"avares://Zametek.Shell.ProjectPlan/Themes/AppResources.axaml") });
        }
    }

    public static class TestAppBuilder
    {
        public static AppBuilder BuildAvaloniaApp()
        {
            // Before UseReactiveUI, as the heads do it: see CompositionRoot.Configure.
            CompositionRoot.Configure(new TestPlatformModule());

            return AppBuilder.Configure<HeadlessTestApp>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .WithInterFont()
                .UseReactiveUI(builder => builder.WithAvalonia());
        }
    }

    /// <summary>
    /// The services that a head supplies: settings in memory, and dialogs that answer yes and remember the errors.
    /// </summary>
    public sealed class TestPlatformModule
        : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<BrowserSettingService>().As<ISettingService>().SingleInstance();
            builder.RegisterType<TestDialogService>().As<IDialogService>().AsSelf().SingleInstance();
            builder.RegisterType<UnavailableMicrosoftProjectFileImporter>().As<IMicrosoftProjectFileImporter>().SingleInstance();
        }
    }

    public sealed class TestDialogService
        : IDialogService
    {
        public List<string> Errors { get; } = [];

        public object Parent { set { } }

        public Task ShowNotificationAsync(string title, string header, string message) => Task.CompletedTask;

        public Task ShowErrorAsync(string title, string header, string message)
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }

        public Task ShowWarningAsync(string title, string header, string message) => Task.CompletedTask;

        public Task ShowInfoAsync(string title, string header, string message, bool showMainPageLink = false) => Task.CompletedTask;

        public Task ShowInfoAsync(string title, string header, string message, double height, double width, bool showMainPageLink = false) => Task.CompletedTask;

        public Task<bool> ShowContextAsync(string title, string header, string message, object context) => Task.FromResult(true);

        public Task<bool> ShowContextAsync(string title, string header, string message, object context, double height, double width) => Task.FromResult(true);

        public Task<bool> ShowConfirmationAsync(string title, string header, string message) => Task.FromResult(true);

        public Task<string?> ShowOpenFileDialogAsync(string initialDirectory, IList<IFileFilter> fileFilters) => Task.FromResult<string?>(null);

        public Task<string?> ShowSaveFileDialogAsync(string initialFilename, string initialDirectory, IList<IFileFilter> fileFilters) => Task.FromResult<string?>(null);
    }

    /// <summary>
    /// One headless session and one application for all the tests: the composition root is static, and Avalonia has one UI
    /// thread for each process.
    /// </summary>
    public sealed class ApplicationFixture
        : IDisposable
    {
        private ApplicationDriver? m_Driver;

        public HeadlessUnitTestSession Session { get; } = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder), AvaloniaTestIsolationLevel.PerAssembly);

        public List<string> UnhandledExceptions { get; } = [];

        /// <summary>
        /// Runs a test on the UI thread, with a new project open.
        /// </summary>
        public Task RunAsync(Func<ApplicationDriver, Task> test) =>
            Session.Dispatch(async () =>
            {
                ApplicationDriver driver = m_Driver ??= await StartAsync();

                try
                {
                    await driver.OpenPlanAsync();
                    await test(driver);
                    driver.ShouldHaveNoErrors();
                }
                finally
                {
                    // The application stays for the next test, and an error stays with the test that it failed.
                    driver.ForgetErrors();
                }

                return 0;
            }, CancellationToken.None);

        private async Task<ApplicationDriver> StartAsync()
        {
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                UnhandledExceptions.Add(e.Exception.ToString());
                e.Handled = true;
            };

            await Task.Run(CompositionRoot.Build);

            IContainer container = CompositionRoot.Container;
            IMainViewModel main = container.Resolve<IMainViewModel>();

            if (((MainViewModel)main).Layout is null)
            {
                main.RestoreLayout();
            }

            var window = new Window
            {
                Width = 1400,
                Height = 900,
                Content = new MainView
                {
                    DataContext = main,
                    InitialTheme = container.Resolve<ISettingService>().SelectedTheme,
                    CanExit = false,
                },
            };

            window.Show();

            return new ApplicationDriver(window, container, this);
        }

        public void Dispose() => Session.Dispose();
    }

    [CollectionDefinition(Name)]
    public sealed class ApplicationCollection
        : ICollectionFixture<ApplicationFixture>
    {
        public const string Name = @"Application";
    }
}
