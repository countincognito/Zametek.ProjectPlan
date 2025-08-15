using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using ScottPlot.Avalonia;
using Zametek.Common.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class PlotHelper
    {
        public static AvaPlot SetBaseTheme(
            this AvaPlot plotModel,
            BaseTheme baseTheme)
        {
            return baseTheme switch
            {
                BaseTheme.Light => plotModel.SetLightTheme(),
                BaseTheme.Dark => plotModel.SetDarkTheme(),
                _ => throw new ArgumentOutOfRangeException(nameof(baseTheme), baseTheme, null),
            };
        }

        public static AvaPlot SetLightTheme(this AvaPlot plotModel) =>
            plotModel.SetTheme(
                ColorHelper.ScottPlotLightThemeForegroundColor,
                ColorHelper.ScottPlotLightThemeBackgroundColor);

        public static AvaPlot SetDarkTheme(this AvaPlot plotModel) =>
            plotModel.SetTheme(
                ColorHelper.ScottPlotDarkThemeForegroundColor,
                ColorHelper.ScottPlotDarkThemeBackgroundColor);

        public static AvaPlot SetTheme(
            this AvaPlot plotModel,
            ScottPlot.Color foregroundColor,
            ScottPlot.Color backgroundColor)
        {
            // Change figure colors.
            plotModel.Plot.FigureBackground.Color = backgroundColor;
            plotModel.Plot.DataBackground.Color = ScottPlot.Colors.Transparent;

            // Change axis and grid colors.
            plotModel.Plot.Axes.Color(foregroundColor);
            plotModel.Plot.Grid.MajorLineColor = foregroundColor;

            // Change legend colors.
            plotModel.Plot.Legend.BackgroundColor = ScottPlot.Colors.Transparent;
            plotModel.Plot.Legend.FontColor = foregroundColor;
            plotModel.Plot.Legend.OutlineColor = foregroundColor;

            foreach (ScottPlot.IPlottable plottable in plotModel.Plot.GetPlottables())
            {
                plottable.TypeSwitchOn()
                    .Case<ScottPlot.Plottables.HorizontalLine>(x =>
                    {
                        x.Color = foregroundColor;
                        x.LabelFontColor = foregroundColor;
                    })
                    .Case<ScottPlot.Plottables.VerticalLine>(x =>
                    {
                        x.Color = foregroundColor;
                        x.LabelFontColor = foregroundColor;
                    });
            }





            //plot.Background = background;
            //plot.PlotAreaBackground = background;
            //plot.PlotAreaBorderColor = foreground;
            //plot.TitleColor = foreground;
            //plot.SubtitleColor = foreground;
            //plot.TextColor = foreground;

            //foreach (LegendBase? legend in plot.Legends)
            //{
            //    legend.LegendBorder = foreground;
            //    legend.LegendBackground = background;
            //    legend.TextColor = foreground;
            //    legend.LegendTextColor = foreground;
            //    legend.LegendTitleColor = foreground;
            //}

            //foreach (Annotation? annotation in plot.Annotations)
            //{
            //    annotation.TypeSwitchOn()
            //        .Case<RectangleAnnotation>(x =>
            //        {
            //            x.TextColor = foreground;
            //            x.Stroke = foreground;
            //        })
            //        .Case<LineAnnotation>(x =>
            //        {
            //            x.Color = foreground;
            //        });
            //}

            //foreach (Axis? axis in plot.Axes)
            //{
            //    axis.TicklineColor = foreground;
            //    axis.TextColor = foreground;
            //    axis.AxislineColor = foreground;
            //    axis.MajorGridlineColor = OxyColor.FromAColor(ColorHelper.AnnotationALight, foreground);
            //    axis.MinorGridlineColor = OxyColor.FromAColor(ColorHelper.AnnotationALight, foreground);
            //    axis.TitleColor = foreground;
            //}

            //foreach (Series? series in plot.Series)
            //{
            //    series.TypeSwitchOn()
            //        .Case<IntervalBarSeries>(x =>
            //        {
            //            x.StrokeColor = OxyColor.FromAColor(ColorHelper.AnnotationAHeavy, foreground);
            //        })
            //        .Case<AreaSeries>(x => { })
            //        .Case<LineSeries>(x => { });
            //}

            return plotModel;
        }

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
