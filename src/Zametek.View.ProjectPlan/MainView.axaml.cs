using Avalonia.Controls;
using Avalonia.Input;
using ReactiveUI;
using System;
using System.Reactive.Linq;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class MainView
        : Window
    {
        private IDisposable? m_UpdateCursorSub;

        public MainView()
        {
            InitializeComponent();
            Loaded += MainView_Loaded;
            Unloaded += MainView_Unloaded;
        }

        private void MainView_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var vm = DataContext as IMainViewModel;
            m_UpdateCursorSub = vm.WhenAnyValue(x => x.IsBusy)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(UpdateCursor);
        }

        private void MainView_Unloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            m_UpdateCursorSub?.Dispose();
        }

        private void UpdateCursor(bool show)
        {
            Cursor = show ? new Cursor(StandardCursorType.Wait) : Cursor.Default;
        }
    }
}
