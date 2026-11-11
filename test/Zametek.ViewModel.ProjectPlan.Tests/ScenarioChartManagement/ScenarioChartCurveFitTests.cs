using ScottPlot;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    // The fitted curves of the scenario chart. A fit with n parameters needs n different values on the X axis. The chart
    // once counted the points, and so four tracked scenarios with three different durations gave a singular cubic fit and
    // a curve with no meaning (the tests of the zpp skill, 2026-11-11). When the different values are too few, the chart
    // shows only the points: no formula and no curve.
    public class ScenarioChartCurveFitTests
    {
        // Each fit, with the number of its parameters.
        public static TheoryData<CurveFittingType, int> Fits => new()
        {
            { CurveFittingType.Linear, 2 },
            { CurveFittingType.Exponential, 2 },
            { CurveFittingType.Logarithmic, 2 },
            { CurveFittingType.Power, 2 },
            { CurveFittingType.PolynomialOrder2, 3 },
            { CurveFittingType.PolynomialOrder3, 4 },
            { CurveFittingType.PolynomialOrder4, 5 },
        };

        // Points with positive values, as a duration and a risk have, so that each fit can take them.
        private static List<AnnotatedMarker> Markers(params double[] xs) =>
            [.. xs.Select((x, i) => new AnnotatedMarker { X = x, Y = 0.5 + (0.03 * i) + (0.001 * x * x) })];

        // As many points as the fit has parameters, but with two of them at the same X: one different value too few.
        private static List<AnnotatedMarker> TooFewDifferentValues(int parameters) =>
            Markers([.. Enumerable.Range(0, parameters).Select(i => 10.0 + (2 * System.Math.Max(0, i - 1)))]);

        // As many points as the fit has parameters, each at a different X.
        private static List<AnnotatedMarker> EnoughDifferentValues(int parameters) =>
            Markers([.. Enumerable.Range(0, parameters).Select(i => 10.0 + (2 * i))]);

        private static (string Formula, int Curves) Fit(List<AnnotatedMarker> markers, CurveFittingType fit, bool derivative)
        {
            ChartFonts.Register();
            using var plot = new Plot();
            string formula = derivative
                ? ScenarioChartManagerViewModel.BuildCurveFitDerivative(plot, markers, fit, false, plot.Axes.Left, Colors.Blue)
                : ScenarioChartManagerViewModel.BuildCurveFit(plot, markers, fit, false, plot.Axes.Left, Colors.Blue);
            return (formula, plot.GetPlottables().Count());
        }

        [Theory]
        [MemberData(nameof(Fits))]
        public void BuildCurveFit_Given_TooFewDifferentXValues_Then_NoFormulaAndNoCurve(CurveFittingType fit, int parameters)
        {
            List<AnnotatedMarker> markers = TooFewDifferentValues(parameters);
            markers.Count.ShouldBe(parameters);
            markers.Select(x => x.X).Distinct().Count().ShouldBe(parameters - 1);

            (string formula, int curves) = Fit(markers, fit, derivative: false);

            formula.ShouldBeEmpty();
            curves.ShouldBe(0);
        }

        [Theory]
        [MemberData(nameof(Fits))]
        public void BuildCurveFitDerivative_Given_TooFewDifferentXValues_Then_NoFormulaAndNoCurve(CurveFittingType fit, int parameters)
        {
            (string formula, int curves) = Fit(TooFewDifferentValues(parameters), fit, derivative: true);

            formula.ShouldBeEmpty();
            curves.ShouldBe(0);
        }

        [Theory]
        [MemberData(nameof(Fits))]
        public void BuildCurveFit_Given_EnoughDifferentXValues_Then_AFormulaAndACurve(CurveFittingType fit, int parameters)
        {
            (string formula, int curves) = Fit(EnoughDifferentValues(parameters), fit, derivative: false);

            formula.ShouldNotBeEmpty();
            curves.ShouldBe(1);
        }

        [Theory]
        [MemberData(nameof(Fits))]
        public void BuildCurveFitDerivative_Given_EnoughDifferentXValues_Then_AFormulaAndACurve(CurveFittingType fit, int parameters)
        {
            (string formula, int curves) = Fit(EnoughDifferentValues(parameters), fit, derivative: true);

            formula.ShouldNotBeEmpty();
            curves.ShouldBe(1);
        }
    }
}
