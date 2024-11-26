using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using Zametek.Common.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class PlotHelper
    {
        public static PlotModel SetBaseTheme(
            this PlotModel plot,
            BaseTheme baseTheme)
        {
            if (baseTheme == BaseTheme.Light)
            {
                return plot.SetLightTheme();
            }
            if (baseTheme == BaseTheme.Dark)
            {
                return plot.SetDarkTheme();
            }
            return plot;
        }

        public static PlotModel SetLightTheme(this PlotModel plot) =>
            plot.SetTheme(OxyColors.Black, ColorHelper.OxyLightThemeBackground);

        public static PlotModel SetDarkTheme(this PlotModel plot) =>
            plot.SetTheme(OxyColors.White, ColorHelper.OxyDarkThemeBackground);

        public static PlotModel SetTheme(
            this PlotModel plot,
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
                legend.LegendBackground = background;
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

            return plot;
        }
    }
}
