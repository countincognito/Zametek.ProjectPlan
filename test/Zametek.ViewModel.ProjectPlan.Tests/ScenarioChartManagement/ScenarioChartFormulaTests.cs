using ScottPlot;
using Shouldly;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    // The formula of each fit, as the panel shows it below the chart. The fits and their formulas come from one calculation,
    // which also gives the data of the chart, and these tests keep the text of each formula as it was before that change.
    // The culture is the invariant culture, because the panel writes the numbers in the culture of the user.
    public class ScenarioChartFormulaTests
    {
        public static TheoryData<CurveFittingType, bool, bool, string> Formulas => new()
        {
            { CurveFittingType.Linear, false, false, "y = 0.096x + -0.40000 (r²=0.94932)" },
            { CurveFittingType.Linear, true, false, "y′ = 0.096 (r²=0.94932)" },
            { CurveFittingType.Linear, false, true, "y = |0.096x + -0.40000| (r²=0.94932)" },
            { CurveFittingType.Linear, true, true, "y′ = |0.096| (r²=0.94932)" },
            { CurveFittingType.Exponential, false, false, "y = 0.21777e^0.10173x (r²=0.98067)" },
            { CurveFittingType.Exponential, true, false, "y′ = 0.022153e^0.10173x (r²=0.98067)" },
            { CurveFittingType.Logarithmic, false, false, "y = 1.28104ln(x) + -2.40963 (r²=0.91167)" },
            { CurveFittingType.Logarithmic, true, false, "y′ = 1.28104/x (r²=0.91167)" },
            { CurveFittingType.Power, false, false, "y = 0.024945x^1.37169 (r²=0.95997)" },
            { CurveFittingType.Power, true, false, "y′ = 0.034216x^0.37169 (r²=0.95997)" },
            { CurveFittingType.PolynomialOrder2, false, false, "y = 0.0078571x² - 0.12400x¹ + 1.07714 (r²=0.98493)" },
            { CurveFittingType.PolynomialOrder2, true, false, "y′ = 0.015714x¹ - 0.12400 (r²=0.98493)" },
            { CurveFittingType.PolynomialOrder3, false, false, "y = 0.0011458x³ - 0.040268x² + 0.53417x¹ - 1.84886 (r²=0.98805)" },
            { CurveFittingType.PolynomialOrder3, true, false, "y′ = 0.0034375x² - 0.080536x¹ + 0.53417 (r²=0.98805)" },
            { CurveFittingType.PolynomialOrder3, false, true, "y = |0.0011458x³ - 0.040268x² + 0.53417x¹ - 1.84886| (r²=0.98805)" },
            { CurveFittingType.PolynomialOrder4, false, false, "y = 0.0014844x⁴ - 0.081979x³ + 1.67906x² - 15.02208x¹ + 50.07000 (r²=1.00000)" },
            { CurveFittingType.PolynomialOrder4, true, false, "y′ = 0.0059375x³ - 0.24594x² + 3.35813x¹ - 15.02208 (r²=1.00000)" },
        };

        // Five points with positive values, at five different values of X, as five tracked scenarios give. They are on no
        // curve of a low order, and so no coefficient of a fit is a rounding error near 0.
        private static readonly double[] s_Ys = [0.62, 0.71, 0.93, 1.05, 1.41];

        private static List<AnnotatedMarker> Markers() =>
            [.. new[] { 10.0, 12.0, 14.0, 16.0, 18.0 }.Select((x, i) => new AnnotatedMarker { X = x, Y = s_Ys[i] })];

        [Theory]
        [MemberData(nameof(Formulas))]
        public void BuildCurveFit_Given_AFit_Then_TheFormulaIsAsBefore(CurveFittingType fit, bool derivative, bool absolute, string expected)
        {
            CultureInfo culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                ChartFonts.Register();
                using var plot = new Plot();

                string formula = derivative
                    ? ScenarioChartManagerViewModel.BuildCurveFitDerivative(plot, Markers(), fit, absolute, plot.Axes.Left, Colors.Blue)
                    : ScenarioChartManagerViewModel.BuildCurveFit(plot, Markers(), fit, absolute, plot.Axes.Left, Colors.Blue);

                formula.ShouldBe(expected);
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        }
    }
}
