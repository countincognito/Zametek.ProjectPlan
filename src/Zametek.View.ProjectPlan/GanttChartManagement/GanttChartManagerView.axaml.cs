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
    public partial class GanttChartManagerView
        : UserControl
    {
        private IDisposable? m_UpdateGanttChartSub;
        private GanttChartManagerViewModel? m_ViewModel;

        public GanttChartManagerView()
        {
            InitializeComponent();
            oxyplot.ActualController.UnbindMouseDown(OxyMouseButton.Right);
            oxyplot.ActualController.UnbindMouseDown(OxyMouseButton.Left);
            oxyplot.ActualController.BindMouseDown(OxyMouseButton.Left, PlotCommands.PanAt);
            //oxyplot.ActualController.BindMouseEnter(PlotCommands.HoverTrack);

            Loaded += GanttChartManagerView_Loaded;
            Unloaded += GanttChartManagerView_Unloaded;
        }

        private void GanttChartManagerView_Loaded(
            object? sender,
            RoutedEventArgs e)
        {
            m_ViewModel = DataContext as GanttChartManagerViewModel;
            if (m_ViewModel is not null)
            {
                m_UpdateGanttChartSub = m_ViewModel.WhenAnyValue(
                    vm => vm.GanttChartPlotModel,
                    vm => vm.SelectedTheme)
                    .ObserveOn(RxApp.TaskpoolScheduler)
                    .Subscribe(UpdateGanttChart);
            }
        }

        private void GanttChartManagerView_Unloaded(
            object? sender,
            RoutedEventArgs e)
        {
            m_UpdateGanttChartSub?.Dispose();
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
