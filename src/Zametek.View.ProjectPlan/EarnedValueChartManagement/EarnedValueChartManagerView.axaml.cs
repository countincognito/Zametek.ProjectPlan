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
    public partial class EarnedValueChartManagerView
        : UserControl
    {
        private IDisposable? m_UpdateEarnedValueChartSub;
        private EarnedValueChartManagerViewModel? m_ViewModel;

        public EarnedValueChartManagerView()
        {
            InitializeComponent();
            oxyplot.ActualController.UnbindMouseDown(OxyMouseButton.Right);
            oxyplot.ActualController.UnbindMouseWheel();

            Loaded += EarnedValueChartManagerView_Loaded;
            Unloaded += EarnedValueChartManagerView_Unloaded;
        }


        private void EarnedValueChartManagerView_Loaded(
            object? sender,
            RoutedEventArgs e)
        {
            m_ViewModel = DataContext as EarnedValueChartManagerViewModel;
            if (m_ViewModel is not null)
            {
                m_UpdateEarnedValueChartSub = m_ViewModel.WhenAnyValue(
                    vm => vm.EarnedValueChartPlotModel,
                    vm => vm.SelectedTheme)
                    .ObserveOn(RxApp.TaskpoolScheduler)
                    .Subscribe(UpdateGanttChart);
            }
        }

        private void EarnedValueChartManagerView_Unloaded(
            object? sender,
            RoutedEventArgs e)
        {
            m_UpdateEarnedValueChartSub?.Dispose();
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
