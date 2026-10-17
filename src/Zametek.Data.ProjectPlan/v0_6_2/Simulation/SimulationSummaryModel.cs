namespace Zametek.Data.ProjectPlan.v0_6_2
{
    // What the last run of the schedule simulation of a scenario found: the statistics of the finish time, a
    // histogram that is split by critical path, the critical paths, and how often each activity was critical.
    // It does not hold the iterations. It is new in v0.6.2.
    [Serializable]
    public record SimulationSummaryModel
    {
        public int IterationCount { get; init; }

        public int ValidIterationCount { get; init; }

        public int FailedIterationCount { get; init; }

        public int PlannedFinishTime { get; init; }

        public int? TargetFinishTime { get; init; }

        public double? TargetProbability { get; init; }

        public double Mean { get; init; }

        public double StandardDeviation { get; init; }

        public int Minimum { get; init; }

        public int Maximum { get; init; }

        public List<SimulationPercentileModel> Percentiles { get; init; } = [];

        public int BinWidth { get; init; } = 1;

        public List<SimulationHistogramBinModel> Bins { get; init; } = [];

        public List<SimulationPathModel> Paths { get; init; } = [];

        public int ChartPathCount { get; init; }

        public double OtherPathsWeight { get; init; }

        public List<SimulationActivityCriticalityModel> Activities { get; init; } = [];
    }

    [Serializable]
    public record SimulationPercentileModel
    {
        public int Percentile { get; init; }

        public int FinishTime { get; init; }
    }

    [Serializable]
    public record SimulationHistogramBinModel
    {
        public int FirstFinishTime { get; init; }

        public int LastFinishTime { get; init; }

        public int Count { get; init; }

        public double CumulativeShare { get; init; }

        public List<double> PathGroupWeights { get; init; } = [];
    }

    [Serializable]
    public record SimulationPathModel
    {
        public int Rank { get; init; }

        public string Label { get; init; } = string.Empty;

        public string ShortLabel { get; init; } = string.Empty;

        public string IdLabel { get; init; } = string.Empty;

        public bool HasResourceLinks { get; init; }

        public double Weight { get; init; }

        public double Share { get; init; }
    }

    [Serializable]
    public record SimulationActivityCriticalityModel
    {
        public int ActivityId { get; init; }

        public string Name { get; init; } = string.Empty;

        public int CriticalIterationCount { get; init; }

        public double CriticalityIndex { get; init; }
    }
}
