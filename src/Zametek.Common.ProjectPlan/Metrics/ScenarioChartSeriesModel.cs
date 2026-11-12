namespace Zametek.Common.ProjectPlan
{
    // An axis of Y of the scenario chart: its metric, the fit that the chart asks for, whether the chart shows the absolute
    // value of the curve, the points in the order of the chart (by X, and then by Y), and the fit. An axis has no fit when it
    // asks for none, when the points have too few different values of X, or when the fit is not a curve (for example, an
    // exponential fit of a value of 0).
    [Serializable]
    public record ScenarioChartSeriesModel
    {
        public TrackedMetrics Metric { get; init; }

        public CurveFittingType CurveFittingType { get; init; }

        public bool AbsoluteCurveFitting { get; init; }

        public IReadOnlyList<ScenarioChartPointModel> Points { get; init; } = [];

        public CurveFitModel? CurveFit { get; init; }

        // Two axes are equal when they have the same values: a record would compare the list of points by reference.
        public virtual bool Equals(ScenarioChartSeriesModel? other)
        {
            return other is not null
                && Metric == other.Metric
                && CurveFittingType == other.CurveFittingType
                && AbsoluteCurveFitting == other.AbsoluteCurveFitting
                && Points.SequenceEqual(other.Points)
                && Equals(CurveFit, other.CurveFit);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Metric);
            hash.Add(CurveFittingType);
            hash.Add(AbsoluteCurveFitting);
            hash.Add(CurveFit);

            foreach (ScenarioChartPointModel point in Points)
            {
                hash.Add(point);
            }

            return hash.ToHashCode();
        }
    }
}
