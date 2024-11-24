using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class PlotHelper
    {
        public static void SetLightTheme(PlotModel plot) => SetTheme(plot, OxyColors.Black, OxyColors.White);

        public static void SetDarkTheme(PlotModel plot) => SetTheme(plot, OxyColors.White, OxyColors.Black);

        public static void SetTheme(
            PlotModel plot,
            OxyColor foreground,
            OxyColor background)
        {
            plot.Background = background;
            plot.PlotAreaBackground = background;
            plot.PlotAreaBorderColor = foreground;
            plot.TitleColor = foreground;
            plot.SubtitleColor = foreground;
            plot.TextColor = foreground;

            foreach (LegendBase? legend in plot.Legends)
            {
                legend.LegendBorder = foreground;
                legend.LegendBackground = OxyColor.FromAColor(ColorHelper.AnnotationALegend, background);
                legend.TextColor = foreground;
                legend.LegendTextColor = foreground;
                legend.LegendTitleColor = foreground;
            }

            foreach (Annotation? annotation in plot.Annotations)
            {
                annotation.TypeSwitchOn()
                    .Case<RectangleAnnotation>(x =>
                    {
                        x.TextColor = foreground;
                        x.Stroke = foreground;
                    })
                    .Case<LineAnnotation>(x =>
                    {
                        x.Color = foreground;
                    });
            }

            foreach (Axis? axis in plot.Axes)
            {
                axis.TicklineColor = foreground;
                axis.TextColor = foreground;
                axis.AxislineColor = foreground;
                axis.MajorGridlineColor = OxyColor.FromAColor(ColorHelper.AnnotationALight, foreground);
                axis.MinorGridlineColor = OxyColor.FromAColor(ColorHelper.AnnotationALight, foreground);
                axis.TitleColor = foreground;
            }

            foreach (Series? series in plot.Series)
            {
                series.TypeSwitchOn()
                    .Case<IntervalBarSeries>(x =>
                    {
                        x.StrokeColor = OxyColor.FromAColor(ColorHelper.AnnotationAHeavy, foreground);
                    })
                    .Case<AreaSeries>(x => { })
                    .Case<LineSeries>(x => { });
            }
        }
    }
}
