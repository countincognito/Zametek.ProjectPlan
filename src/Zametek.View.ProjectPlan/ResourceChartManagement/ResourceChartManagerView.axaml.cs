using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using OxyPlot;
using ReactiveUI;
using System;
using System.Reactive.Linq;
using Zametek.Utility;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class ResourceChartManagerView
        : UserControl
    {
        private IDisposable? m_UpdateResourceChartSub;
        private ResourceChartManagerViewModel? m_ViewModel;

        public ResourceChartManagerView()
        {
            InitializeComponent();
            oxyplot.ActualController.UnbindMouseDown(OxyMouseButton.Right);
            oxyplot.ActualController.UnbindMouseWheel();

            Loaded += ResourceChartManagerView_Loaded;
            Unloaded += ResourceChartManagerView_Unloaded;
        }

        private void ResourceChartManagerView_Loaded(
            object? sender,
            RoutedEventArgs e)
        {
            m_ViewModel = DataContext as ResourceChartManagerViewModel;
            if (m_ViewModel is not null)
            {
                m_UpdateResourceChartSub = m_ViewModel.WhenAnyValue(
                    vm => vm.ResourceChartPlotModel,
                    vm => vm.SelectedTheme)
                    .ObserveOn(RxApp.TaskpoolScheduler)
                    .Subscribe(UpdateGanttChart);
            }
        }

        private void ResourceChartManagerView_Unloaded(
            object? sender,
            RoutedEventArgs e)
        {
            m_UpdateResourceChartSub?.Dispose();
        }

        private void UpdateGanttChart((PlotModel plot, string theme) input)
        {
            ThemeVariant inheritedThemeVariant = ThemeHelper.GetInheritedThemeVariant(input.theme);

            inheritedThemeVariant.ValueSwitchOn()
                .Case(ThemeVariant.Light, _ => PlotHelper.SetLightTheme(input.plot))
                .Case(ThemeVariant.Dark, _ => PlotHelper.SetDarkTheme(input.plot));

            oxyplot.InvalidatePlot();
        }
    }
}
