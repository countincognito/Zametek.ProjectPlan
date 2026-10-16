namespace Zametek.Common.ProjectPlan
{
    /// <summary>
    /// What a simulation of the schedule says, made from the raw result: the statistics of the finish
    /// time, a histogram split by critical path, the critical paths, and how often each activity was
    /// critical. All the times are in days from the start of the project.
    /// </summary>
    [Serializable]
    public record SimulationSummaryModel
    {
        /// <summary>
        /// The number of iterations that ran, including the ones that failed.
        /// </summary>
        public int IterationCount { get; init; }

        /// <summary>
        /// The iterations that gave a finish time. Every statistic uses these.
        /// </summary>
        public int ValidIterationCount { get; init; }

        /// <summary>
        /// The iterations that failed: the resource scheduler could not place every activity, or the
        /// schedule ran past the limit.
        /// </summary>
        public int FailedIterationCount { get; init; }

        /// <summary>
        /// The finish time of the plan as it stands, with the expected duration of each activity.
        /// </summary>
        public int PlannedFinishTime { get; init; }

        /// <summary>
        /// The target finish time that the user gave, or null.
        /// </summary>
        public int? TargetFinishTime { get; init; }

        /// <summary>
        /// The share of the valid iterations that finish at the target or before it, or null when there is
        /// no target.
        /// </summary>
        public double? TargetProbability { get; init; }

        public double Mean { get; init; }

        public double StandardDeviation { get; init; }

        public int Minimum { get; init; }

        public int Maximum { get; init; }

        /// <summary>
        /// The finish times at the percentiles that the summary reports, in order of the percentile. A
        /// percentile is the smallest finish time that at least that share of the iterations reach (the
        /// nearest rank).
        /// </summary>
        public IReadOnlyList<SimulationPercentileModel> Percentiles { get; init; } = [];

        /// <summary>
        /// The number of days that one bin of the histogram covers. It is 1 while the finish times fit in
        /// the bins.
        /// </summary>
        public int BinWidth { get; init; } = 1;

        public IReadOnlyList<SimulationHistogramBinModel> Bins { get; init; } = [];

        /// <summary>
        /// The critical paths, from the one with the most weight to the one with the least, up to a limit.
        /// When an iteration has more than one critical path, its weight is split equally among them.
        /// </summary>
        public IReadOnlyList<SimulationPathModel> Paths { get; init; } = [];

        /// <summary>
        /// The number of paths that the histogram shows by themselves. The bins hold one more group after
        /// these, for all the other paths.
        /// </summary>
        public int ChartPathCount { get; init; }

        /// <summary>
        /// The weight, in iterations, of the paths that are not in <see cref="Paths"/>, and of the
        /// iterations that have no path.
        /// </summary>
        public double OtherPathsWeight { get; init; }

        /// <summary>
        /// How often each activity was critical, from the most often to the least. An activity that was
        /// never critical is not in the list.
        /// </summary>
        public IReadOnlyList<SimulationActivityCriticalityModel> Activities { get; init; } = [];

        public bool HasResult => ValidIterationCount > 0;
    }

    [Serializable]
    public record SimulationPercentileModel
    {
        /// <summary>
        /// The percentile, from 1 to 99.
        /// </summary>
        public int Percentile { get; init; }

        public int FinishTime { get; init; }
    }

    [Serializable]
    public record SimulationHistogramBinModel
    {
        /// <summary>
        /// The first finish time that the bin covers.
        /// </summary>
        public int FirstFinishTime { get; init; }

        /// <summary>
        /// The last finish time that the bin covers.
        /// </summary>
        public int LastFinishTime { get; init; }

        /// <summary>
        /// The number of iterations that finish in the bin.
        /// </summary>
        public int Count { get; init; }

        /// <summary>
        /// The share of the valid iterations that finish at the last day of the bin or before it. This is
        /// the S-curve.
        /// </summary>
        public double CumulativeShare { get; init; }

        /// <summary>
        /// The weight of each group of paths in the bin, in the order of the paths and then the group of
        /// all the other paths. The weights add up to <see cref="Count"/>.
        /// </summary>
        public IReadOnlyList<double> PathGroupWeights { get; init; } = [];
    }

    [Serializable]
    public record SimulationPathModel
    {
        /// <summary>
        /// The place of the path in the list, from 1.
        /// </summary>
        public int Rank { get; init; }

        /// <summary>
        /// The names of the activities on the path. A link that exists because two activities share a
        /// resource is written with ~&gt;, and a dependency of the plan with &gt;.
        /// </summary>
        public string Label { get; init; } = string.Empty;

        /// <summary>
        /// The same as <see cref="Label"/> for a path of up to six activities, and the first two and the
        /// last two for a longer path.
        /// </summary>
        public string ShortLabel { get; init; } = string.Empty;

        /// <summary>
        /// The same as <see cref="Label"/> with the ID of each activity in the place of its name: 1 > 3 ~> 7.
        /// It is short, and a table that has a lot of paths needs that.
        /// </summary>
        public string IdLabel { get; init; } = string.Empty;

        public bool HasResourceLinks { get; init; }

        /// <summary>
        /// The weight of the path in iterations: one for each iteration where it is the only critical path,
        /// and a share for each iteration where it is one of several.
        /// </summary>
        public double Weight { get; init; }

        /// <summary>
        /// The weight as a share of the valid iterations.
        /// </summary>
        public double Share { get; init; }
    }

    [Serializable]
    public record SimulationActivityCriticalityModel
    {
        public int ActivityId { get; init; }

        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// The number of valid iterations in which the activity was critical.
        /// </summary>
        public int CriticalIterationCount { get; init; }

        /// <summary>
        /// The share of the valid iterations in which the activity was critical.
        /// </summary>
        public double CriticalityIndex { get; init; }
    }
}
