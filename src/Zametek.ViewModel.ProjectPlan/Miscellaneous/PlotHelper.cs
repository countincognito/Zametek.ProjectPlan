using ScottPlot;
using ScottPlot.Avalonia;
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
            Color foregroundColor,
            Color backgroundColor)
        {
            // Change figure colors.
            if (plotModel.Plot.FigureBackground.Color != Colors.Transparent)
            {
                plotModel.Plot.FigureBackground.Color = backgroundColor;
            }

            if (plotModel.Plot.DataBackground.Color != Colors.Transparent)
            {
                plotModel.Plot.DataBackground.Color = backgroundColor;
            }

            // Change axis and grid colors.
            plotModel.Plot.Axes.Color(foregroundColor);

            if (plotModel.Plot.Grid.MajorLineColor != Colors.Transparent)
            {
                plotModel.Plot.Grid.MajorLineColor = foregroundColor.WithAlpha(ColorHelper.AnnotationALight);
                plotModel.Plot.Grid.MinorLineColor = foregroundColor.WithAlpha(ColorHelper.AnnotationALight);
            }

            // Change legend colors.
            plotModel.Plot.Legend.BackgroundColor = Colors.Transparent;

            if (plotModel.Plot.Legend.FontColor != Colors.Transparent)
            {
                plotModel.Plot.Legend.FontColor = foregroundColor;
            }

            if (plotModel.Plot.Legend.OutlineColor != Colors.Transparent)
            {
                plotModel.Plot.Legend.OutlineColor = foregroundColor;
            }

            // Holiday colors.
            Color holidayForegroundColor = foregroundColor;
            Color holidayBackgroundColor = foregroundColor;

            // Change plottable colors.
            foreach (IPlottable plottable in plotModel.Plot.GetPlottables())
            {
                plottable.TypeSwitchOn()
                    .Case<ScottPlot.Plottables.AxisLine>(x =>
                    {
                        if (x.Color != Colors.Transparent)
                        {
                            x.Color = foregroundColor;
                        }
                        if (x.LabelFontColor != Colors.Transparent)
                        {
                            x.LabelFontColor = foregroundColor;
                        }
                        if (x.LabelBackgroundColor != Colors.Transparent)
                        {
                            x.LabelBackgroundColor = backgroundColor;
                        }
                    })
                    .Case<ScottPlot.Plottables.Text>(x =>
                    {
                        if (x.LabelFontColor != Colors.Transparent)
                        {
                            x.LabelFontColor = foregroundColor;
                        }
                        if (x.LabelBackgroundColor != Colors.Transparent)
                        {
                            x.LabelBackgroundColor = backgroundColor;
                        }
                    })
                    .Case<ScottPlot.Plottables.Annotation>(x =>
                    {
                        if (x.LabelFontColor != Colors.Transparent)
                        {
                            x.LabelFontColor = foregroundColor;
                        }
                        if (x.LabelBackgroundColor != Colors.Transparent)
                        {
                            x.LabelBackgroundColor = backgroundColor;
                        }
                        if (x.LabelBorderColor != Colors.Transparent)
                        {
                            x.LabelBorderColor = foregroundColor;
                        }
                        if (x.LabelShadowColor != Colors.Transparent)
                        {
                            x.LabelShadowColor = backgroundColor;
                        }
                    })
                    .Case<HolidayRectangle>(x =>
                    {
                        if (x.LineColor != Colors.Transparent)
                        {
                            x.LineColor = holidayForegroundColor;
                        }
                        if (x.FillColor != Colors.Transparent)
                        {
                            x.FillColor = holidayBackgroundColor.WithAlpha(ColorHelper.AnnotationAHoliday);
                        }
                    })
                    .Case<ScottPlot.Plottables.Rectangle>(x =>
                    {
                        if (x.LineColor != Colors.Transparent)
                        {
                            x.LineColor = foregroundColor;
                        }
                    })
                    .Case<ScottPlot.Plottables.Arrow>(x =>
                    {
                        if (x.ArrowLineColor != Colors.Transparent)
                        {
                            x.ArrowLineColor = foregroundColor;
                        }
                    })
                    .Case<ScottPlot.Plottables.Ellipse>(x =>
                    {
                        if (x.LineColor != Colors.Transparent)
                        {
                            x.LineColor = foregroundColor;
                        }
                    })
                    .Case<ScottPlot.Plottables.BarPlot>(x =>
                    {
                        foreach (Bar bar in x.Bars)
                        {
                            if (bar.LineColor != Colors.Transparent)
                            {
                                bar.LineColor = foregroundColor.WithAlpha(ColorHelper.AnnotationAHeavy);
                            }
                        }
                    });
            }

            return plotModel;
        }
    }
}
