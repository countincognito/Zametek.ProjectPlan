using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    // The fits of the scenario chart as numbers: the coefficients of each curve, its r², and the coefficients of its
    // derivative. The chart, its formulas and the data of the chart all come from them. Points that are on a curve give the
    // coefficients of that curve and an r² of 1.
    public class CurveFitCalculatorTests
    {
        private const double c_Tolerance = 1e-9;

        private static readonly double[] s_Xs = [1.0, 2.0, 3.0, 4.0, 5.0];

        // Points on no curve of the fits, so that each fit is a curve of its own.
        private static readonly double[] s_Ys = [0.62, 0.71, 0.93, 1.05, 1.41];

        private static CurveFitModel FitOf(CurveFittingType curveFittingType, Func<double, double> curve)
        {
            CurveFitModel? fit = CurveFitCalculator.Fit(s_Xs, [.. s_Xs.Select(curve)], curveFittingType);
            fit.ShouldNotBeNull();
            fit.CurveFittingType.ShouldBe(curveFittingType);
            fit.RSquared.ShouldBe(1.0, c_Tolerance);
            return fit;
        }

        private static void ShouldBe(IReadOnlyList<double> actual, double[] expected, double tolerance = c_Tolerance)
        {
            actual.Count.ShouldBe(expected.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                actual[i].ShouldBe(expected[i], tolerance);
            }
        }

        [Fact]
        public void Fit_Given_PointsOnALine_Then_TheInterceptAndTheSlope()
        {
            // y = c0 + c1·x, and y′ = d0.
            CurveFitModel fit = FitOf(CurveFittingType.Linear, x => 2.0 + (3.0 * x));

            ShouldBe(fit.Coefficients, [2.0, 3.0]);
            ShouldBe(fit.DerivativeCoefficients, [3.0]);
        }

        [Fact]
        public void Fit_Given_PointsOnAnExponentialCurve_Then_ItsFactorAndItsRate()
        {
            // y = c0·e^(c1·x), and y′ = d0·e^(d1·x).
            CurveFitModel fit = FitOf(CurveFittingType.Exponential, x => 2.0 * Math.Exp(0.1 * x));

            ShouldBe(fit.Coefficients, [2.0, 0.1]);
            ShouldBe(fit.DerivativeCoefficients, [0.2, 0.1]);
        }

        [Fact]
        public void Fit_Given_PointsOnALogarithmicCurve_Then_ItsConstantAndItsFactor()
        {
            // y = c0 + c1·ln(x), and y′ = d0 / x.
            CurveFitModel fit = FitOf(CurveFittingType.Logarithmic, x => 1.0 + (2.0 * Math.Log(x)));

            ShouldBe(fit.Coefficients, [1.0, 2.0]);
            ShouldBe(fit.DerivativeCoefficients, [2.0]);
        }

        [Fact]
        public void Fit_Given_PointsOnAPowerCurve_Then_ItsFactorAndItsExponent()
        {
            // y = c0·x^c1, and y′ = d0·x^d1.
            CurveFitModel fit = FitOf(CurveFittingType.Power, x => 3.0 * Math.Pow(x, 2.0));

            ShouldBe(fit.Coefficients, [3.0, 2.0]);
            ShouldBe(fit.DerivativeCoefficients, [6.0, 1.0]);
        }

        [Fact]
        public void Fit_Given_PointsOnPolynomials_Then_TheCoefficientsFromTheConstantUp()
        {
            // y = Σ ci·x^i, and y′ = Σ di·x^i.
            CurveFitModel second = FitOf(CurveFittingType.PolynomialOrder2, x => 1.0 + (2.0 * x) + (3.0 * x * x));
            ShouldBe(second.Coefficients, [1.0, 2.0, 3.0]);
            ShouldBe(second.DerivativeCoefficients, [2.0, 6.0]);

            CurveFitModel third = FitOf(CurveFittingType.PolynomialOrder3, x => 1.0 - x + (0.5 * x * x) + (0.25 * x * x * x));
            ShouldBe(third.Coefficients, [1.0, -1.0, 0.5, 0.25], 1e-7);
            ShouldBe(third.DerivativeCoefficients, [-1.0, 1.0, 0.75], 1e-7);

            CurveFitModel fourth = FitOf(CurveFittingType.PolynomialOrder4, x => 1.0 + x + (x * x) + (x * x * x) + (x * x * x * x));
            ShouldBe(fourth.Coefficients, [1.0, 1.0, 1.0, 1.0, 1.0], 1e-6);
            ShouldBe(fourth.DerivativeCoefficients, [1.0, 2.0, 3.0, 4.0], 1e-6);
        }

        [Fact]
        public void Fit_Given_NoCurveFitting_Then_NoFit()
        {
            CurveFitCalculator.Fit(s_Xs, s_Ys, CurveFittingType.None).ShouldBeNull();
        }

        [Theory]
        [MemberData(nameof(ScenarioChartCurveFitTests.Fits), MemberType = typeof(ScenarioChartCurveFitTests))]
        public void Fit_Given_OneDifferentValueOfXTooFew_Then_NoFit(CurveFittingType curveFittingType, int parameters)
        {
            // As many points as the fit has parameters, but two of them at the same X.
            double[] xs = [.. Enumerable.Range(0, parameters).Select(i => 10.0 + (2 * Math.Max(0, i - 1)))];

            CurveFitCalculator.Fit(xs, [.. xs.Select((x, i) => 1.0 + i)], curveFittingType).ShouldBeNull();
        }

        [Theory]
        [InlineData(CurveFittingType.Exponential)]
        [InlineData(CurveFittingType.Power)]
        public void Fit_Given_AValueOfYThatTheCurveCannotHave_Then_NoFit(CurveFittingType curveFittingType)
        {
            // These fits take the logarithm of each value of Y, and the logarithm of 0 is not a number. The fit is then not a
            // curve, and the chart shows only the points.
            CurveFitCalculator.Fit(s_Xs, [0.0, 1.0, 2.0, 3.0, 4.0], curveFittingType).ShouldBeNull();
        }

        [Theory]
        [InlineData(CurveFittingType.Logarithmic)]
        [InlineData(CurveFittingType.Power)]
        public void Fit_Given_AValueOfXThatTheCurveCannotHave_Then_NoFit(CurveFittingType curveFittingType)
        {
            CurveFitCalculator.Fit([0.0, 1.0, 2.0, 3.0, 4.0], s_Ys, curveFittingType).ShouldBeNull();
        }

        [Fact]
        public void Fit_Given_TheSameValueOfYAtEachPoint_Then_AFlatLineWithNoRSquared()
        {
            // r² is the square of the correlation, and a flat line has no correlation with anything. The line is still a fit.
            CurveFitModel? fit = CurveFitCalculator.Fit(s_Xs, [2.0, 2.0, 2.0, 2.0, 2.0], CurveFittingType.Linear);

            fit.ShouldNotBeNull();
            ShouldBe(fit.Coefficients, [2.0, 0.0]);
            double.IsNaN(fit.RSquared).ShouldBeTrue();
        }

        [Fact]
        public void Fit_Given_FewerValuesOfYThanOfX_Then_Throws()
        {
            Should.Throw<ArgumentException>(() => CurveFitCalculator.Fit(s_Xs, [1.0, 2.0], CurveFittingType.Linear));
        }

        public static TheoryData<CurveFittingType> EachCurveFitting => new()
        {
            CurveFittingType.Linear,
            CurveFittingType.Exponential,
            CurveFittingType.Logarithmic,
            CurveFittingType.Power,
            CurveFittingType.PolynomialOrder2,
            CurveFittingType.PolynomialOrder3,
            CurveFittingType.PolynomialOrder4,
        };

        [Theory]
        [MemberData(nameof(EachCurveFitting))]
        public void EvaluateDerivative_Given_AFit_Then_TheSlopeOfTheCurve(CurveFittingType curveFittingType)
        {
            CurveFitModel? fit = CurveFitCalculator.Fit(s_Xs, s_Ys, curveFittingType);
            fit.ShouldNotBeNull();

            foreach (double x in new[] { 1.5, 2.5, 4.5 })
            {
                const double h = 1e-5;
                double slope = (CurveFitCalculator.Evaluate(fit, x + h) - CurveFitCalculator.Evaluate(fit, x - h)) / (2 * h);

                CurveFitCalculator.EvaluateDerivative(fit, x).ShouldBe(slope, 1e-6);
            }
        }

        [Theory]
        [MemberData(nameof(EachCurveFitting))]
        public void Evaluate_Given_AFit_Then_ItsRSquaredIsTheSquareOfTheCorrelationOfItsValuesWithThePoints(CurveFittingType curveFittingType)
        {
            CurveFitModel? fit = CurveFitCalculator.Fit(s_Xs, s_Ys, curveFittingType);
            fit.ShouldNotBeNull();

            double correlation = MathNet.Numerics.Statistics.Correlation.Pearson(s_Ys, s_Xs.Select(x => CurveFitCalculator.Evaluate(fit, x)));

            fit.RSquared.ShouldBe(correlation * correlation, 1e-12);
        }
    }
}
