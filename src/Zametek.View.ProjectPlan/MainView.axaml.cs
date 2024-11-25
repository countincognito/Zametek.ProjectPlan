using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using ReactiveUI;
using System;
using System.Reactive.Linq;
using Ursa.Controls;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Utility;

namespace Zametek.View.ProjectPlan
{
    public partial class MainView
        : Window
    {
        private IDisposable? m_UpdateCursorSub;
        private IDisposable? m_UpdateThemeSub;
        private IDisposable? m_CompilationErrorSub;
        private IMainViewModel? m_ViewModel;
        private WindowToastManager? m_ToastManager;
        const int c_MaxToastItems = 3;

        public MainView()
        {
            InitializeComponent();
            Loaded += MainView_Loaded;
            Unloaded += MainView_Unloaded;
            InitialTheme = string.Empty;
        }

        // This has to be set here because of how the ThemeToggleButton loads.
        // Even when TwoWay binding is in place, it still forces an initial value of 'Light'.
        public string InitialTheme { get; set; }

        // https://github.com/irihitech/Ursa.Avalonia/blob/main/demo/Ursa.Demo/Pages/ToastDemo.axaml.cs
        private void MainView_Loaded(
            object? sender,
            EventArgs e)
        {
            m_ViewModel = DataContext as IMainViewModel;
            if (m_ViewModel is not null)
            {
                var topLevel = GetTopLevel(this);
                m_ToastManager = new WindowToastManager(topLevel)
                {
                    MaxItems = c_MaxToastItems
                };

                m_UpdateCursorSub = m_ViewModel.WhenAnyValue(
                    main => main.IsBusy,
                    main => main.IsOpening,
                    main => main.IsSaving,
                    main => main.IsSavingAs,
                    main => main.IsImporting,
                    main => main.IsExporting,
                    main => main.IsClosing,
                    (isBusy, isOpening, isSaving, isSavingAs, isImporting, isExporting, isClosing) =>
                        isBusy || isOpening || isSaving || isSavingAs || isImporting || isExporting || isClosing)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(UpdateCursor);

                m_UpdateThemeSub = m_ViewModel.WhenAnyValue(main => main.SelectedTheme)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(UpdateTheme);

                m_CompilationErrorSub = m_ViewModel.WhenAnyValue(main => main.HasCompilationErrors)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(ShowCompilationError);

                m_ViewModel.SelectedTheme = InitialTheme;
            }
        }

        private void MainView_Unloaded(
            object? sender,
            RoutedEventArgs e)
        {
            m_UpdateCursorSub?.Dispose();
            m_UpdateThemeSub?.Dispose();
            m_CompilationErrorSub?.Dispose();
            m_ToastManager?.Uninstall();
        }

        private void UpdateCursor(bool show)
        {
            Cursor = show ? new Cursor(StandardCursorType.Wait) : Cursor.Default;
            LoadingPanel.IsLoading = show;
        }

        private void UpdateTheme(string theme)
        {
            var app = Application.Current;
            if (app is not null)
            {
                app.RequestedThemeVariant = ThemeHelper.GetThemeVariant(theme);
            }
            if (m_ViewModel is not null)
            {
                ThemeVariant inheritedThemeVariant = ThemeHelper.GetInheritedThemeVariant(theme);
                BaseTheme baseTheme = BaseTheme.Light;

                inheritedThemeVariant.ValueSwitchOn()
                    .Case(ThemeVariant.Light, _ => baseTheme = BaseTheme.Light)
                    .Case(ThemeVariant.Dark, _ => baseTheme = BaseTheme.Dark);

                m_ViewModel.BaseTheme = baseTheme;
            }
        }

        // https://github.com/irihitech/Ursa.Avalonia/blob/main/demo/Ursa.Demo/ViewModels/ToastDemoViewModel.cs
        private void ShowCompilationError(bool hasCompilationErrors)
        {
            if (hasCompilationErrors)
            {
                ThemeVariant inheritedThemeVariant = ThemeHelper.GetInheritedThemeVariant(m_ViewModel?.SelectedTheme);

                m_ToastManager?.Show(
                    new Toast(Resource.ProjectPlan.Messages.Message_CompilationErrors),
                    showIcon: true,
                    showClose: true,
                    type: NotificationType.Error,
                    classes: [inheritedThemeVariant.ToString() ?? Resource.ProjectPlan.Themes.Theme_Default]);
            }
        }
    }
}
