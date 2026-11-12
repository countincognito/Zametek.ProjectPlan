namespace Zametek.Common.ProjectPlan
{
    // A point of the scenario chart: a tracked scenario, by its id and its path, and the values of the metrics of the axes. A
    // metric that the scenario does not have is 0, as on the chart.
    [Serializable]
    public record ScenarioChartPointModel
    {
        public Guid Id { get; init; }

        public string Path { get; init; } = string.Empty;

        public double X { get; init; }

        public double Y { get; init; }
    }
}
