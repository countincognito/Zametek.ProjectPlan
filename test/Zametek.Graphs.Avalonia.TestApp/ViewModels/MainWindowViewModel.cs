using Avalonia;
using Avalonia.Styling;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Zametek.Graphs.Avalonia;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace Zametek.Graphs.Avalonia.TestApp.ViewModels
{
    // Shell view-model: builds the four demonstration tabs (default arrow / vertex, then bespoke arrow /
    // vertex) and drives the light/dark toggle. Toggling flips both the whole app's Fluent theme variant
    // and every tab's graph theme in step, so the window chrome and the graph canvases match and the
    // theme-aware image export can be seen to follow.
    public class MainWindowViewModel
        : ViewModelBase, IDisposable
    {
        private readonly IReadOnlyList<GraphTabViewModelBase> m_Tabs;
        private bool m_Disposed;

        public MainWindowViewModel()
        {
            // Start from a known, consistent baseline (Light) rather than the system default, so the graph
            // theme and the app variant always agree.
            const GraphTheme initialTheme = GraphTheme.Light;
            m_IsDark = initialTheme == GraphTheme.Dark;

            m_Tabs =
            [
                new ArrowDefaultGraphTabViewModel(initialTheme),
                new VertexDefaultGraphTabViewModel(initialTheme),
                new ArrowBespokeGraphTabViewModel(initialTheme),
                new VertexBespokeGraphTabViewModel(initialTheme),
            ];
            Tabs = new ObservableCollection<GraphTabViewModelBase>(m_Tabs);

            ToggleThemeCommand = ReactiveCommand.Create(ToggleTheme);

            ApplyAppThemeVariant();
        }

        public ObservableCollection<GraphTabViewModelBase> Tabs { get; }

        private bool m_IsDark;
        public bool IsDark
        {
            get => m_IsDark;
            private set => this.RaiseAndSetIfChanged(ref m_IsDark, value);
        }

        public string ThemeButtonText => IsDark ? @"Switch to light" : @"Switch to dark";

        public ReactiveCommand<RxVoid, RxVoid> ToggleThemeCommand { get; }

        private void ToggleTheme()
        {
            IsDark = !IsDark;
            this.RaisePropertyChanged(nameof(ThemeButtonText));

            GraphTheme theme = IsDark ? GraphTheme.Dark : GraphTheme.Light;
            foreach (GraphTabViewModelBase tab in Tabs)
            {
                tab.ApplyTheme(theme);
            }
            ApplyAppThemeVariant();
        }

        // Keep the whole app's Fluent theme in step with the graph theme.
        private void ApplyAppThemeVariant()
        {
            if (Application.Current is { } app)
            {
                app.RequestedThemeVariant = IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
            }
        }

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }
            m_Disposed = true;

            ToggleThemeCommand.Dispose();
            foreach (GraphTabViewModelBase tab in Tabs)
            {
                tab.Dispose();
            }
        }
    }
}
