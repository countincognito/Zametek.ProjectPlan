using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using Ursa.Controls;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Utility;

namespace Zametek.View.ProjectPlan
{
    public class PaletteCommand
    {
        public string Label { get; }
        public string Gesture { get; }
        public ICommand UnderlyingCommand { get; }
        public ICommand ExecuteCommand { get; }

        public PaletteCommand(string label, string gesture, ICommand command, Action? onExecuted = null)
        {
            Label = label;
            Gesture = gesture;
            UnderlyingCommand = command;
            ExecuteCommand = new RelayCommand(() =>
            {
                if (command.CanExecute(null))
                    command.Execute(null);
                onExecuted?.Invoke();
            });
        }
    }

    public partial class MainView
        : Window
    {
        private IDisposable? m_UpdateCursorSub;
        private IDisposable? m_UpdateThemeSub;
        private IDisposable? m_CompilationErrorSub;
        private IMainViewModel? m_ViewModel;
        private WindowToastManager? m_ToastManager;
        const int c_MaxToastItems = 3;

        private List<PaletteCommand> m_AllPaletteCommands = [];
        private List<PaletteCommand> m_FilteredPaletteCommands = [];

        public MainView()
        {
            InitializeComponent();
            Loaded += MainView_Loaded;
            Unloaded += MainView_Unloaded;
            InitialTheme = string.Empty;

            // Ctrl+K — open command palette
            KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(Key.K, KeyModifiers.Control),
                Command = new RelayCommand(() =>
                {
                    if (m_ViewModel is not null)
                    {
                        m_ViewModel.IsCommandPaletteOpen = !m_ViewModel.IsCommandPaletteOpen;
                        if (m_ViewModel.IsCommandPaletteOpen)
                        {
                            OpenPalette();
                        }
                    }
                })
            });
        }

        // This has to be set here because of how the ThemeToggleButton loads.
        // Even when TwoWay binding is in place, it still forces an initial value of 'Light'.
        public string InitialTheme { get; set; }

        private void BuildPaletteCommands()
        {
            if (m_ViewModel is null) return;

            m_AllPaletteCommands =
            [
                new PaletteCommand("Open Project", "Ctrl+O", m_ViewModel.OpenProjectFileCommand, ClosePalette),
                new PaletteCommand("Save Project", "Ctrl+S", m_ViewModel.SaveProjectFileCommand, ClosePalette),
                new PaletteCommand("Save Project As", "", m_ViewModel.SaveAsProjectFileCommand, ClosePalette),
                new PaletteCommand("Import Scenario", "", m_ViewModel.ImportProjectScenarioFileCommand, ClosePalette),
                new PaletteCommand("Export Scenario", "", m_ViewModel.ExportProjectScenarioFileCommand, ClosePalette),
                new PaletteCommand("Close Project", "", m_ViewModel.CloseProjectCommand, ClosePalette),
                new PaletteCommand("Compile", "", m_ViewModel.CompileCommand, ClosePalette),
                new PaletteCommand("Transitive Reduction", "", m_ViewModel.TransitiveReductionCommand, ClosePalette),
                new PaletteCommand("Toggle Dates", "Ctrl+D", m_ViewModel.ToggleShowDatesCommand, ClosePalette),
                new PaletteCommand("Toggle Cost", "Ctrl+W", m_ViewModel.ToggleHideCostCommand, ClosePalette),
                new PaletteCommand("Toggle Billing", "Ctrl+E", m_ViewModel.ToggleHideBillingCommand, ClosePalette),
                new PaletteCommand("Toggle Auto-Compile", "", m_ViewModel.ToggleAutoCompileCommand, ClosePalette),
                new PaletteCommand("Switch to Activities + Gantt", "", new RelayCommand(() => m_ViewModel.ActiveShellView = ShellView.ActivitiesGantt), ClosePalette),
                new PaletteCommand("Switch to Resources", "", new RelayCommand(() => m_ViewModel.ActiveShellView = ShellView.Resources), ClosePalette),
                new PaletteCommand("Switch to Scenario", "", new RelayCommand(() => m_ViewModel.ActiveShellView = ShellView.Scenario), ClosePalette),
                new PaletteCommand("Switch to Arrow Graph", "", new RelayCommand(() => m_ViewModel.ActiveShellView = ShellView.ArrowGraph), ClosePalette),
                new PaletteCommand("Switch to Vertex Graph", "", new RelayCommand(() => m_ViewModel.ActiveShellView = ShellView.VertexGraph), ClosePalette),
                new PaletteCommand("Switch to Tracking", "", new RelayCommand(() => m_ViewModel.ActiveShellView = ShellView.Tracking), ClosePalette),
                new PaletteCommand("Switch to Resource Settings", "", new RelayCommand(() => m_ViewModel.ActiveShellView = ShellView.ResourceSettings), ClosePalette),
                new PaletteCommand("Switch to Work Streams", "", new RelayCommand(() => m_ViewModel.ActiveShellView = ShellView.WorkStreams), ClosePalette),
                new PaletteCommand("Switch to Graph Settings", "", new RelayCommand(() => m_ViewModel.ActiveShellView = ShellView.GraphSettings), ClosePalette),
                new PaletteCommand("Switch to Holidays", "", new RelayCommand(() => m_ViewModel.ActiveShellView = ShellView.Holidays), ClosePalette),
                new PaletteCommand("Switch to Metrics", "", new RelayCommand(() => m_ViewModel.ActiveShellView = ShellView.Metrics), ClosePalette),
                new PaletteCommand("Switch to Earned Value", "", new RelayCommand(() => m_ViewModel.ActiveShellView = ShellView.EarnedValue), ClosePalette),
                new PaletteCommand("Switch to Project Scenario", "", new RelayCommand(() => m_ViewModel.ActiveShellView = ShellView.ProjectScenario), ClosePalette),
                new PaletteCommand("Switch to Output", "", new RelayCommand(() => m_ViewModel.ActiveShellView = ShellView.Output), ClosePalette),
                new PaletteCommand("Add Activity", "", m_ViewModel.ActivitiesManagerViewModel.AddManagedActivityCommand, ClosePalette),
                new PaletteCommand("Documentation", "", m_ViewModel.OpenDocumentationCommand, ClosePalette),
                new PaletteCommand("Report Issue", "", m_ViewModel.OpenReportIssueCommand, ClosePalette),
                new PaletteCommand("About", "", m_ViewModel.OpenAboutCommand, ClosePalette),
            ];

            m_FilteredPaletteCommands = [.. m_AllPaletteCommands];
        }

        private void OpenPalette()
        {
            FilterPalette(string.Empty);
            PaletteSearchBox?.Focus();
            if (PaletteSearchBox is not null)
                PaletteSearchBox.Text = string.Empty;
        }

        private void FilterPalette(string? query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                m_FilteredPaletteCommands = [.. m_AllPaletteCommands];
            }
            else
            {
                string lower = query.ToLowerInvariant();
                m_FilteredPaletteCommands = m_AllPaletteCommands
                    .Where(c => c.Label.Contains(lower, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (PaletteItemsControl is not null)
                PaletteItemsControl.ItemsSource = m_FilteredPaletteCommands;
        }

        private void ClosePalette()
        {
            if (m_ViewModel is not null)
                m_ViewModel.IsCommandPaletteOpen = false;
        }

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
                    // TODO
                    //main => main.IsImporting,
                    //main => main.IsExporting,
                    main => main.IsClosing,
                    (isBusy, isOpening, isSaving, isSavingAs, //isImporting, isExporting,
                    isClosing) =>
                        isBusy || isOpening || isSaving || isSavingAs //|| isImporting || isExporting
                        || isClosing)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(UpdateCursor);

                m_UpdateThemeSub = m_ViewModel.WhenAnyValue(main => main.SelectedTheme)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(UpdateTheme);

                m_CompilationErrorSub = m_ViewModel.WhenAnyValue(main => main.HasCompilationErrors)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(ShowCompilationError);

                m_ViewModel.SelectedTheme = InitialTheme;

                BuildPaletteCommands();

                // Wire up command palette trigger click
                if (CommandPaletteTrigger is not null)
                {
                    CommandPaletteTrigger.PointerPressed += (_, _) =>
                    {
                        m_ViewModel.IsCommandPaletteOpen = true;
                        OpenPalette();
                    };
                }
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

        // ── Nav button click handlers ──

        private void NavActivitiesGantt_Click(object? sender, RoutedEventArgs e)
        {
            if (m_ViewModel is not null) m_ViewModel.ActiveShellView = ShellView.ActivitiesGantt;
        }

        private void NavResources_Click(object? sender, RoutedEventArgs e)
        {
            if (m_ViewModel is not null) m_ViewModel.ActiveShellView = ShellView.Resources;
        }

        private void NavScenario_Click(object? sender, RoutedEventArgs e)
        {
            if (m_ViewModel is not null) m_ViewModel.ActiveShellView = ShellView.Scenario;
        }

        private void NavArrowGraph_Click(object? sender, RoutedEventArgs e)
        {
            if (m_ViewModel is not null) m_ViewModel.ActiveShellView = ShellView.ArrowGraph;
        }

        private void NavVertexGraph_Click(object? sender, RoutedEventArgs e)
        {
            if (m_ViewModel is not null) m_ViewModel.ActiveShellView = ShellView.VertexGraph;
        }

        private void NavTracking_Click(object? sender, RoutedEventArgs e)
        {
            if (m_ViewModel is not null) m_ViewModel.ActiveShellView = ShellView.Tracking;
        }

        private void NavResourceSettings_Click(object? sender, RoutedEventArgs e)
        {
            if (m_ViewModel is not null) m_ViewModel.ActiveShellView = ShellView.ResourceSettings;
        }

        private void NavWorkStreams_Click(object? sender, RoutedEventArgs e)
        {
            if (m_ViewModel is not null) m_ViewModel.ActiveShellView = ShellView.WorkStreams;
        }

        private void NavGraphSettings_Click(object? sender, RoutedEventArgs e)
        {
            if (m_ViewModel is not null) m_ViewModel.ActiveShellView = ShellView.GraphSettings;
        }

        private void NavHolidays_Click(object? sender, RoutedEventArgs e)
        {
            if (m_ViewModel is not null) m_ViewModel.ActiveShellView = ShellView.Holidays;
        }

        private void NavMetrics_Click(object? sender, RoutedEventArgs e)
        {
            if (m_ViewModel is not null) m_ViewModel.ActiveShellView = ShellView.Metrics;
        }

        private void NavEarnedValue_Click(object? sender, RoutedEventArgs e)
        {
            if (m_ViewModel is not null) m_ViewModel.ActiveShellView = ShellView.EarnedValue;
        }

        private void NavProjectScenario_Click(object? sender, RoutedEventArgs e)
        {
            if (m_ViewModel is not null) m_ViewModel.ActiveShellView = ShellView.ProjectScenario;
        }

        private void NavOutput_Click(object? sender, RoutedEventArgs e)
        {
            if (m_ViewModel is not null) m_ViewModel.ActiveShellView = ShellView.Output;
        }

        // ── Palette handlers ──

        private void PaletteSearch_TextChanged(object? sender, TextChangedEventArgs e)
        {
            FilterPalette((sender as TextBox)?.Text);
        }

        private void PaletteSearch_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ClosePalette();
                e.Handled = true;
            }
        }

        // Clicking outside the palette closes it
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (m_ViewModel?.IsCommandPaletteOpen == true)
            {
                // Check if click is inside the palette border
                var paletteVisual = this.FindControl<Border>("PaletteOverlay");
                if (paletteVisual is not null)
                {
                    var pt = e.GetPosition(paletteVisual);
                    var bounds = paletteVisual.Bounds;
                    if (pt.X < 0 || pt.Y < 0 || pt.X > bounds.Width || pt.Y > bounds.Height)
                    {
                        ClosePalette();
                    }
                }
            }
        }
    }

    /// <summary>Minimal relay command for palette items that have no ViewModel backing.</summary>
    internal sealed class RelayCommand : ICommand
    {
        private readonly Action m_Execute;

        public RelayCommand(Action execute) => m_Execute = execute;

#pragma warning disable CS0067
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => m_Execute();
    }
}
