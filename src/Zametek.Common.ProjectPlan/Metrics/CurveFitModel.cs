namespace Zametek.Common.ProjectPlan
{
    // A curve that is fitted to the points of an axis of the scenario chart, and its derivative. The coefficients follow
    // the type of the fit, and the derivative has the same form as the curve, except where it says otherwise:
    // - Linear: y = c0 + c1·x, and y′ = d0.
    // - Exponential: y = c0·e^(c1·x), and y′ = d0·e^(d1·x).
    // - Logarithmic: y = c0 + c1·ln(x), and y′ = d0 / x.
    // - Power: y = c0·x^c1, and y′ = d0·x^d1.
    // - Polynomial: y = Σ ci·x^i, from the constant up, and y′ = Σ di·x^i.
    // r² is the square of the correlation of the curve with the points. It is not a number when the points have one value of
    // Y, and the curve is then flat.
    [Serializable]
    public record CurveFitModel
    {
        public CurveFittingType CurveFittingType { get; init; }

        public IReadOnlyList<double> Coefficients { get; init; } = [];

        public IReadOnlyList<double> DerivativeCoefficients { get; init; } = [];

        public double RSquared { get; init; }

        // Two fits are equal when they have the same values: a record would compare the lists by reference.
        public virtual bool Equals(CurveFitModel? other)
        {
            return other is not null
                && CurveFittingType == other.CurveFittingType
                && Coefficients.SequenceEqual(other.Coefficients)
                && DerivativeCoefficients.SequenceEqual(other.DerivativeCoefficients)
                && RSquared.Equals(other.RSquared);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(CurveFittingType);
            hash.Add(RSquared);

            foreach (double coefficient in Coefficients)
            {
                hash.Add(coefficient);
            }

            foreach (double coefficient in DerivativeCoefficients)
            {
                hash.Add(coefficient);
            }

            return hash.ToHashCode();
        }
    }
}
