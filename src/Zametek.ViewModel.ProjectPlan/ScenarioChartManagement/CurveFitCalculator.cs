using System.Globalization;
using System.Text;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// The fits of the scenario chart: the coefficients of a curve through the points of an axis, its r², the coefficients of
    /// its derivative, the values of the curve and of its derivative, and the formulas that the panel shows. The chart, the
    /// formulas and the data of the chart all come from here, and so they always agree. <see cref="CurveFitModel"/> gives
    /// the form of each curve.
    /// </summary>
    public static class CurveFitCalculator
    {
        private static readonly char[] s_PolynomialSuperscripts = ['⁰', '¹', '²', '³', '⁴'];

        // The two ways a formula's numbers can be rounded. Either may be changed on its
        // own; FormatFormulaValue picks whichever of them suits the value in hand.
        private const int c_FormulaValueSignificantFigures = 5;

        private const int c_FormulaValueDecimalPlaces = 5;

        private static readonly string s_FormulaValueFixedFormat = $"F{c_FormulaValueDecimalPlaces}";

        private static readonly string s_FormulaValueGeneralFormat = $"G{c_FormulaValueSignificantFigures}";

        /// <summary>
        /// The fit of a curve of the type through the points, or null when there is no fit. The type can be none. A fit
        /// with n parameters needs n different values on the X axis, not n points: two scenarios with the same duration are
        /// one value, and too few values give a singular fit and a curve with no meaning. A fit that is not a curve - a
        /// coefficient that is not a number, as the logarithm of 0 gives - is no fit either. The chart then shows only the
        /// points.
        /// </summary>
        public static CurveFitModel? Fit(
            IReadOnlyList<double> xs,
            IReadOnlyList<double> ys,
            CurveFittingType curveFittingType)
        {
            ArgumentNullException.ThrowIfNull(xs);
            ArgumentNullException.ThrowIfNull(ys);

            if (xs.Count != ys.Count)
            {
                throw new ArgumentException(null, nameof(ys));
            }

            double[] x = [.. xs];
            double[] y = [.. ys];
            int differentXs = x.Distinct().Count();

            (double[] Coefficients, double[] DerivativeCoefficients)? fit = curveFittingType switch
            {
                CurveFittingType.None => null,
                CurveFittingType.Linear => differentXs >= 2 ? Linear(x, y) : null,
                CurveFittingType.Exponential => differentXs >= 2 ? Exponential(x, y) : null,
                CurveFittingType.Logarithmic => differentXs >= 2 ? Logarithmic(x, y) : null,
                CurveFittingType.Power => differentXs >= 2 ? Power(x, y) : null,
                CurveFittingType.PolynomialOrder2 => differentXs > 2 ? Polynomial(x, y, 2) : null,
                CurveFittingType.PolynomialOrder3 => differentXs > 3 ? Polynomial(x, y, 3) : null,
                CurveFittingType.PolynomialOrder4 => differentXs > 4 ? Polynomial(x, y, 4) : null,
                _ => throw new ArgumentOutOfRangeException(nameof(curveFittingType), @$"{Resource.ProjectPlan.Messages.Message_UnknownCurveFittingType} {curveFittingType}"),
            };

            if (fit is not (double[] coefficients, double[] derivativeCoefficients)
                || !coefficients.All(double.IsFinite)
                || !derivativeCoefficients.All(double.IsFinite))
            {
                return null;
            }

            var model = new CurveFitModel
            {
                CurveFittingType = curveFittingType,
                Coefficients = coefficients,
                DerivativeCoefficients = derivativeCoefficients,
            };

            // The r² describes the fit against the raw points.
            return model with { RSquared = MathNet.Numerics.GoodnessOfFit.RSquared(y, x.Select(value => Evaluate(model, value))) };
        }

        /// <summary>
        /// The value of the curve at x.
        /// </summary>
        public static double Evaluate(
            CurveFitModel fit,
            double x)
        {
            ArgumentNullException.ThrowIfNull(fit);
            IReadOnlyList<double> c = fit.Coefficients;

            return fit.CurveFittingType switch
            {
                CurveFittingType.Linear => c[0] + (c[1] * x),
                CurveFittingType.Exponential => c[0] * Math.Exp(c[1] * x),
                CurveFittingType.Logarithmic => c[0] + (c[1] * Math.Log(x)),
                CurveFittingType.Power => c[0] * Math.Pow(x, c[1]),
                CurveFittingType.PolynomialOrder2
                    or CurveFittingType.PolynomialOrder3
                    or CurveFittingType.PolynomialOrder4 => MathNet.Numerics.Polynomial.Evaluate(x, [.. c]),
                _ => throw new ArgumentOutOfRangeException(nameof(fit), @$"{Resource.ProjectPlan.Messages.Message_UnknownCurveFittingType} {fit.CurveFittingType}"),
            };
        }

        /// <summary>
        /// The value of the derivative of the curve at x.
        /// </summary>
        public static double EvaluateDerivative(
            CurveFitModel fit,
            double x)
        {
            ArgumentNullException.ThrowIfNull(fit);
            IReadOnlyList<double> d = fit.DerivativeCoefficients;

            return fit.CurveFittingType switch
            {
                CurveFittingType.Linear => d[0],
                CurveFittingType.Exponential => d[0] * Math.Exp(d[1] * x),
                CurveFittingType.Logarithmic => d[0] / x,
                CurveFittingType.Power => d[0] * Math.Pow(x, d[1]),
                CurveFittingType.PolynomialOrder2
                    or CurveFittingType.PolynomialOrder3
                    or CurveFittingType.PolynomialOrder4 => MathNet.Numerics.Polynomial.Evaluate(x, [.. d]),
                _ => throw new ArgumentOutOfRangeException(nameof(fit), @$"{Resource.ProjectPlan.Messages.Message_UnknownCurveFittingType} {fit.CurveFittingType}"),
            };
        }

        /// <summary>
        /// The formula of the curve, with its r², as the panel shows it below the chart. The absolute option shows the
        /// absolute value of the curve, as the chart plots it.
        /// </summary>
        public static string BuildFormula(
            CurveFitModel fit,
            bool absoluteCurveFitting)
        {
            ArgumentNullException.ThrowIfNull(fit);
            IReadOnlyList<double> c = fit.Coefficients;

            string expression = fit.CurveFittingType switch
            {
                CurveFittingType.Linear => $"{FormatFormulaValue(c[1])}x + {FormatFormulaValue(c[0])}",
                CurveFittingType.Exponential => $"{FormatFormulaValue(c[0])}e^{FormatFormulaValue(c[1])}x",
                CurveFittingType.Logarithmic => $"{FormatFormulaValue(c[1])}ln(x) + {FormatFormulaValue(c[0])}",
                CurveFittingType.Power => $"{FormatFormulaValue(c[0])}x^{FormatFormulaValue(c[1])}",
                CurveFittingType.PolynomialOrder2
                    or CurveFittingType.PolynomialOrder3
                    or CurveFittingType.PolynomialOrder4 => BuildPolynomialTerms(c),
                _ => throw new ArgumentOutOfRangeException(nameof(fit), @$"{Resource.ProjectPlan.Messages.Message_UnknownCurveFittingType} {fit.CurveFittingType}"),
            };

            return BuildFormula(@"y", expression, absoluteCurveFitting, fit.RSquared);
        }

        /// <summary>
        /// The formula of the derivative of the curve, with the r² of the curve, as the panel shows it below the chart.
        /// </summary>
        public static string BuildDerivativeFormula(
            CurveFitModel fit,
            bool absoluteCurveFitting)
        {
            ArgumentNullException.ThrowIfNull(fit);
            IReadOnlyList<double> d = fit.DerivativeCoefficients;

            string expression = fit.CurveFittingType switch
            {
                CurveFittingType.Linear => $"{FormatFormulaValue(d[0])}",
                CurveFittingType.Exponential => $"{FormatFormulaValue(d[0])}e^{FormatFormulaValue(d[1])}x",
                CurveFittingType.Logarithmic => $"{FormatFormulaValue(d[0])}/x",
                CurveFittingType.Power => $"{FormatFormulaValue(d[0])}x^{FormatFormulaValue(d[1])}",
                CurveFittingType.PolynomialOrder2
                    or CurveFittingType.PolynomialOrder3
                    or CurveFittingType.PolynomialOrder4 => BuildPolynomialTerms(d),
                _ => throw new ArgumentOutOfRangeException(nameof(fit), @$"{Resource.ProjectPlan.Messages.Message_UnknownCurveFittingType} {fit.CurveFittingType}"),
            };

            return BuildFormula(@"y′", expression, absoluteCurveFitting, fit.RSquared);
        }

        private static string BuildFormula(
            string name,
            string expression,
            bool absoluteCurveFitting,
            double rSquared)
        {
            string shown = absoluteCurveFitting ? $"|{expression}|" : expression;
            return $"{name} = {shown} (r²={FormatFormulaValue(rSquared)})";
        }

        // y = c0 + c1·x differentiates to the constant y′ = c1.
        private static (double[], double[]) Linear(double[] xs, double[] ys)
        {
            (double a, double b) = MathNet.Numerics.Fit.Line(xs, ys);
            return ([a, b], [b]);
        }

        // y = ae^rx differentiates to y′ = are^rx.
        private static (double[], double[]) Exponential(double[] xs, double[] ys)
        {
            (double a, double r) = MathNet.Numerics.Fit.Exponential(xs, ys);
            return ([a, r], [a * r, r]);
        }

        // y = a + b·ln(x) differentiates to y′ = b/x.
        private static (double[], double[]) Logarithmic(double[] xs, double[] ys)
        {
            (double a, double b) = MathNet.Numerics.Fit.Logarithm(xs, ys);
            return ([a, b], [b]);
        }

        // y = ax^b differentiates to y′ = abx^(b−1).
        private static (double[], double[]) Power(double[] xs, double[] ys)
        {
            (double a, double b) = MathNet.Numerics.Fit.Power(xs, ys);
            return ([a, b], [a * b, b - 1.0]);
        }

        // Σcᵢxⁱ differentiates to Σi·cᵢx⁽ⁱ⁻¹⁾.
        private static (double[], double[]) Polynomial(double[] xs, double[] ys, int order)
        {
            double[] coefficients = MathNet.Numerics.Fit.Polynomial(xs, ys, order);
            double[] derivativeCoefficients = new double[coefficients.Length - 1];

            for (int i = 1; i < coefficients.Length; i++)
            {
                derivativeCoefficients[i - 1] = i * coefficients[i];
            }

            return (coefficients, derivativeCoefficients);
        }

        private static string BuildPolynomialTerms(IReadOnlyList<double> coefficients)
        {
            var formula = new StringBuilder();

            for (int i = coefficients.Count - 1; i >= 0; i--)
            {
                if (i < coefficients.Count - 1)
                {
                    if (coefficients[i] < 0)
                    {
                        formula.Append(@" - ");
                    }
                    else
                    {
                        formula.Append(@" + ");
                    }
                }
                else if (coefficients[i] < 0)
                {
                    formula.Append('-');
                }

                if (i > 0)
                {
                    formula.Append($"{FormatFormulaValue(Math.Abs(coefficients[i]))}x{s_PolynomialSuperscripts[i]}");
                }
                else
                {
                    formula.Append($"{FormatFormulaValue(Math.Abs(coefficients[i]))}");
                }
            }

            return formula.ToString();
        }

        /// <summary>
        /// Formats a number for the fitted curve and derivative formulae shown on the
        /// chart, keeping either a fixed number of decimal places or a fixed number of
        /// significant figures - whichever of the two says more about the value in hand.
        /// </summary>
        /// <remarks>
        /// A fixed number of decimal places keeps digits down to a fixed size, however
        /// large or small the value is, while significant figures keep them down to a size
        /// that follows the value. So the significant figures are worth having exactly when
        /// they reach further than the decimal places do, which is what the comparison
        /// below asks: a coefficient of 0.000012345 is worth more as five figures than as
        /// five decimal places (which would leave 0.00001), while 620564.12345 is worth
        /// more the other way round (five figures would leave 620560). A value too small to
        /// read as a decimal comes out in scientific notation, which is the general
        /// format's own doing.
        /// <para>
        /// This is also the one place the formulae are formatted, because a format cannot
        /// be given as an interpolation specifier - everything after the colon there is
        /// literal text, so it could not name these constants. The culture is stated rather
        /// than left implicit, but it is the current one, so the formulae read in the
        /// user's own conventions.
        /// </para>
        /// </remarks>
        private static string FormatFormulaValue(double value)
        {
            // Zero has no exponent to reason about, and neither do the non-finite values.
            if (value == 0.0
                || double.IsNaN(value)
                || double.IsInfinity(value))
            {
                return value.ToString(s_FormulaValueFixedFormat, CultureInfo.CurrentCulture);
            }

            int exponent = (int)Math.Floor(Math.Log10(Math.Abs(value)));
            int significantFigureDecimals = c_FormulaValueSignificantFigures - 1 - exponent;

            return significantFigureDecimals > c_FormulaValueDecimalPlaces
                ? value.ToString(s_FormulaValueGeneralFormat, CultureInfo.CurrentCulture)
                : value.ToString(s_FormulaValueFixedFormat, CultureInfo.CurrentCulture);
        }
    }
}
