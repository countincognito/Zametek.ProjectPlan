using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using com.sun.tools.javac.comp;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
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
                    .ObserveOn(RxApp.MainThreadScheduler)
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
                .Case(ThemeVariant.Light,
                _ =>
                {
                    input.plot.Background = OxyColors.Transparent;


                })
                .Case(ThemeVariant.Dark,
                _ =>
                {
                    input.plot.Background = OxyColors.Black;
                    input.plot.PlotAreaBackground = OxyColors.Black;
                    input.plot.PlotAreaBorderColor = OxyColors.White;
                    input.plot.TitleColor = OxyColors.White;
                    input.plot.SubtitleColor = OxyColors.White;


                    foreach (Annotation? annotation in input.plot.Annotations)
                    {
                        if (annotation is RectangleAnnotation rectangle)
                        {
                            rectangle.TextColor = OxyColors.White;
                            rectangle.Stroke = OxyColors.White;
                        }

                    }


                    foreach (Axis? axis in input.plot.Axes)
                    {
                        axis.TicklineColor = OxyColors.White;
                        axis.TextColor = OxyColors.White;
                        axis.AxislineColor = OxyColors.White;
                        axis.MajorGridlineColor = OxyColors.White;
                        axis.MinorGridlineColor = OxyColors.White;
                        axis.TitleColor = OxyColors.White;
                    }


                    foreach (Series? series in input.plot.Series)
                    {
                        if (series is IntervalBarSeries intervalBarSeries)
                        {
                            //foreach (IntervalBarItem? intervalBarItem in intervalBarSeries.Items)
                            //{
                            //    intervalBarItem.
                            //}



                            intervalBarSeries.StrokeColor = OxyColors.White;
                            //rectangle.Stroke = OxyColors.White;
                        }
                    }






                });

        }
    }
}
