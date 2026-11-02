using DynamicData;
using ScottPlot;
using Zametek.Common.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class PlotHelper
    {
        public const int FontSize = 12;
        public const int FontOffset = FontSize + 1;

        public static Plot SetBaseTheme(
            this Plot plotModel,
            BaseTheme baseTheme)
        {
            return baseTheme switch
            {
                BaseTheme.Light => plotModel.SetLightTheme(),
                BaseTheme.Dark => plotModel.SetDarkTheme(),
                _ => throw new ArgumentOutOfRangeException(nameof(baseTheme), baseTheme, null),
            };
        }

        public static Plot SetLightTheme(this Plot plotModel) =>
            plotModel.SetTheme(
                ColorHelper.ScottPlotLightThemeForegroundColor,
                ColorHelper.ScottPlotLightThemeBackgroundColor);

        public static Plot SetDarkTheme(this Plot plotModel) =>
            plotModel.SetTheme(
                ColorHelper.ScottPlotDarkThemeForegroundColor,
                ColorHelper.ScottPlotDarkThemeBackgroundColor);

        public static Plot SetTheme(
            this Plot plotModel,
            Color foregroundColor,
            Color backgroundColor)
        {
            // Change figure colors.
            if (plotModel.FigureBackground.Color != Colors.Transparent)
            {
                plotModel.FigureBackground.Color = backgroundColor;
            }

            if (plotModel.DataBackground.Color != Colors.Transparent)
            {
                plotModel.DataBackground.Color = backgroundColor;
            }

            // Change axis and grid colors.
            plotModel.Axes.Color(foregroundColor);

            if (plotModel.Grid.MajorLineColor != Colors.Transparent)
            {
                plotModel.Grid.MajorLineColor = foregroundColor.WithAlpha(ColorHelper.AnnotationALight);
                plotModel.Grid.MinorLineColor = foregroundColor.WithAlpha(ColorHelper.AnnotationALight);
            }

            // Change legend colors.
            plotModel.Legend.BackgroundColor = Colors.Transparent;

            if (plotModel.Legend.FontColor != Colors.Transparent)
            {
                plotModel.Legend.FontColor = foregroundColor;
            }

            if (plotModel.Legend.OutlineColor != Colors.Transparent)
            {
                plotModel.Legend.OutlineColor = foregroundColor;
            }

            // Holiday colors.
            Color holidayForegroundColor = foregroundColor;
            Color holidayBackgroundColor = foregroundColor;

            // Change plottable colors.
            foreach (IPlottable plottable in plotModel.GetPlottables())
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
