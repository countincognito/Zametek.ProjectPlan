using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using ReactiveUI;
using System;
using System.Reactive.Linq;
using Ursa.Controls;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class MainView
        : Window
    {
        private IDisposable? m_UpdateCursorSub;
        private readonly string m_InitialTheme;

        public MainView(string initialTheme)
        {
            InitializeComponent();
            Loaded += MainView_Loaded;
            Unloaded += MainView_Unloaded;

            var vm = DataContext as IMainViewModel;

            // This has to be set here because of how the ThemeToggleButton loads.
            // Even when TwoWay binding is in place, it still forces an initial value of 'Light'.
            m_InitialTheme = initialTheme;
        }

        private void MainView_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var vm = DataContext as IMainViewModel;
            if (vm is not null)
            {
                m_UpdateCursorSub = vm.WhenAnyValue(
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

                vm.SelectedTheme = m_InitialTheme;
            }
        }

        private void MainView_Unloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            m_UpdateCursorSub?.Dispose();
        }

        private void UpdateCursor(bool show)
        {
            Cursor = show ? new Cursor(StandardCursorType.Wait) : Cursor.Default;
            LoadingPanel.IsLoading = show;
        }
    }
}
