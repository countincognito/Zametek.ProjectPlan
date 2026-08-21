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
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class MainView
        : UserControl
    {
        private IDisposable? m_UpdateCursorSub;
        private IDisposable? m_UpdateThemeSub;
        private IDisposable? m_CompilationErrorSub;
        private IMainViewModel? m_ViewModel;
        private WindowToastManager? m_ToastManager;
        private bool m_CanExit;
        const int c_MaxToastItems = 3;

        public MainView()
        {
            InitializeComponent();
            Loaded += MainView_Loaded;
            Unloaded += MainView_Unloaded;
            InitialTheme = string.Empty;
            m_CanExit = true;
        }

        // This has to be set here because of how the ThemeToggleButton loads.
        // Even when TwoWay binding is in place, it still forces an initial value of 'Light'.
        public string InitialTheme { get; set; }

        /// <summary>
        /// Raised when the user chooses File | Exit. The host decides what that means, because this
        /// control cannot: on the desktop the window closes, which routes through the Closing handlers
        /// and so applies the unsaved-changes confirmation exactly as the title bar's close button does.
        /// </summary>
        public event EventHandler? ExitRequested;

        /// <summary>
        /// Whether the application can be exited from within itself, which is only true where the host
        /// owns a window. A browser tab is closed by the browser, not by the page inside it, so the
        /// browser head clears this and the File menu loses an item that could not have worked.
        /// </summary>
        /// <remarks>
        /// Applied directly to the named elements rather than bound, because the File menu's items live
        /// in a popup whose visual tree is built on demand - a binding to an ancestor of this control is
        /// not reliably resolvable from in there.
        /// </remarks>
        public bool CanExit
        {
            get => m_CanExit;
            set
            {
                m_CanExit = value;
                ExitMenuItem.IsVisible = value;
                ExitSeparator.IsVisible = value;
            }
        }

        // https://github.com/irihitech/Ursa.Avalonia/blob/main/demo/Ursa.Demo/Pages/ToastDemo.axaml.cs
        private void MainView_Loaded(
            object? sender,
            EventArgs e)
        {
            m_ViewModel = DataContext as IMainViewModel;
            if (m_ViewModel is not null)
            {
                // A Loaded that arrives without a matching Unloaded must not orphan the
                // previous subscriptions, so release them before resubscribing.
                m_UpdateCursorSub?.Dispose();
                m_UpdateThemeSub?.Dispose();
                m_CompilationErrorSub?.Dispose();
                m_ToastManager?.Uninstall();

                // Qualified because this control is no longer a TopLevel itself, as it was when it
                // was a Window. On the desktop this resolves to the hosting window; in the browser it
                // is the single view's top level.
                TopLevel? topLevel = TopLevel.GetTopLevel(this);
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
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(UpdateCursor);

                m_UpdateThemeSub = m_ViewModel.WhenAnyValue(main => main.SelectedTheme)
                    .ObserveOn(RxSchedulers.MainThreadScheduler)
                    .Subscribe(UpdateTheme);

                m_CompilationErrorSub = m_ViewModel.WhenAnyValue(main => main.HasCompilationErrors)
                    .ObserveOn(RxSchedulers.MainThreadScheduler)
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

        private void Exit_Click(object? sender, RoutedEventArgs e)
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
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
