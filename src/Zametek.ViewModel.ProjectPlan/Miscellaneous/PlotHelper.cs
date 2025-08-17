using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using ScottPlot.Avalonia;
using ScottPlot.AxisPanels;
using Zametek.Common.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class PlotHelper
    {
        public const int FontSize = 12;
        public const int FontOffset = FontSize + 1;

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
            if (plotModel.Plot.FigureBackground.Color != ScottPlot.Colors.Transparent)
            {
                plotModel.Plot.FigureBackground.Color = backgroundColor;
            }

            if (plotModel.Plot.DataBackground.Color != ScottPlot.Colors.Transparent)
            {
                plotModel.Plot.DataBackground.Color = backgroundColor;
            }

            // Change axis and grid colors.
            plotModel.Plot.Axes.Color(foregroundColor);

            if (plotModel.Plot.Grid.MajorLineColor != ScottPlot.Colors.Transparent)
            {
                plotModel.Plot.Grid.MajorLineColor = foregroundColor.WithAlpha(ColorHelper.AnnotationALight);
                plotModel.Plot.Grid.MinorLineColor = foregroundColor.WithAlpha(ColorHelper.AnnotationALight);
            }

            // Change legend colors.
            plotModel.Plot.Legend.BackgroundColor = ScottPlot.Colors.Transparent;

            if (plotModel.Plot.Legend.FontColor != ScottPlot.Colors.Transparent)
            {
                plotModel.Plot.Legend.FontColor = foregroundColor;
            }

            if (plotModel.Plot.Legend.OutlineColor != ScottPlot.Colors.Transparent)
            {
                plotModel.Plot.Legend.OutlineColor = foregroundColor;
            }

            // Change plottable colors.
            foreach (ScottPlot.IPlottable plottable in plotModel.Plot.GetPlottables())
            {
                plottable.TypeSwitchOn()
                    .Case<ScottPlot.Plottables.AxisLine>(x =>
                    {
                        if (x.Color != ScottPlot.Colors.Transparent)
                        {
                            x.Color = foregroundColor;
                        }
                        if (x.LabelFontColor != ScottPlot.Colors.Transparent)
                        {
                            x.LabelFontColor = foregroundColor;
                        }
                        if (x.LabelBackgroundColor != ScottPlot.Colors.Transparent)
                        {
                            x.LabelBackgroundColor = backgroundColor;
                        }
                    })
                    .Case<ScottPlot.Plottables.Text>(x =>
                    {
                        if (x.LabelFontColor != ScottPlot.Colors.Transparent)
                        {
                            x.LabelFontColor = foregroundColor;
                        }
                        if (x.LabelBackgroundColor != ScottPlot.Colors.Transparent)
                        {
                            x.LabelBackgroundColor = backgroundColor;
                        }
                    })
                    .Case<ScottPlot.Plottables.Annotation>(x =>
                    {
                        if (x.LabelFontColor != ScottPlot.Colors.Transparent)
                        {
                            x.LabelFontColor = foregroundColor;
                        }
                        if (x.LabelBackgroundColor != ScottPlot.Colors.Transparent)
                        {
                            x.LabelBackgroundColor = backgroundColor;
                        }
                        if (x.LabelBorderColor != ScottPlot.Colors.Transparent)
                        {
                            x.LabelBorderColor = foregroundColor;
                        }
                        if (x.LabelShadowColor != ScottPlot.Colors.Transparent)
                        {
                            x.LabelShadowColor = backgroundColor;
                        }
                    })
                    .Case<ScottPlot.Plottables.Rectangle>(x =>
                    {
                        if (x.LineColor != ScottPlot.Colors.Transparent)
                        {
                            x.LineColor = foregroundColor;
                        }
                    })
                    .Case<ScottPlot.Plottables.Arrow>(x =>
                    {
                        if (x.ArrowLineColor != ScottPlot.Colors.Transparent)
                        {
                            x.ArrowLineColor = foregroundColor;
                        }
                    })
                    .Case<ScottPlot.Plottables.Ellipse>(x =>
                    {
                        if (x.LineColor != ScottPlot.Colors.Transparent)
                        {
                            x.LineColor = foregroundColor;
                        }
                    })
                    .Case<ScottPlot.Plottables.BarPlot>(x =>
                    {
                        foreach (ScottPlot.Bar bar in x.Bars)
                        {
                            if (bar.LineColor != ScottPlot.Colors.Transparent)
                            {
                                bar.LineColor = foregroundColor.WithAlpha(ColorHelper.AnnotationAHeavy);
                            }
                        }
                    });
            }

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
