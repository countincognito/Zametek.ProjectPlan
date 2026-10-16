using System.Globalization;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.Engine.ProjectPlan
{
    // What a simulation found, copied out of the simulation view-model before the job's scope - and the view-model with it -
    // goes: the settings it ran with, and the statistics of the finish time. Every time is in days from the start of the
    // project, with the date it falls on in the working calendar of the project. A run in which every iteration failed has
    // no statistics, and so no values for them.
    public record JobSimulationMetrics
    {
        // The iterations that were asked for. ValidIterations of them gave a finish time.
        public int Iterations { get; init; }

        public ulong Seed { get; init; }

        public DurationDistribution Distribution { get; init; }

        // Only the modified beta-PERT distribution has one.
        public double? ModifiedBetaShape { get; init; }

        public int ValidIterations { get; init; }

        public int FailedIterations { get; init; }

        // The finish of the plan as it stands, with the expected duration of each activity.
        public int PlannedFinishDays { get; init; }

        public DateOnly PlannedFinishDate { get; init; }

        public double? MeanDays { get; init; }

        public double? StandardDeviationDays { get; init; }

        public int? EarliestFinishDays { get; init; }

        public DateOnly? EarliestFinishDate { get; init; }

        public int? LatestFinishDays { get; init; }

        public DateOnly? LatestFinishDate { get; init; }

        public IReadOnlyList<JobSimulationPercentile> Percentiles { get; init; } = [];

        public int? TargetFinishDays { get; init; }

        public DateOnly? TargetFinishDate { get; init; }

        // The share of the valid iterations that finish by the target, from 0 to 1.
        public double? TargetProbability { get; init; }

        // The statistics as the panel shows them, as text: the seed and the distribution, then each of the panel's lines.
        public IReadOnlyList<SimulationStatisticModel> Statistics { get; init; } = [];

        // Two sets of metrics are equal when they say the same thing: a record would compare the lists by reference, not by
        // what is in them.
        public virtual bool Equals(JobSimulationMetrics? other)
        {
            return other is not null
                && Iterations == other.Iterations
                && Seed == other.Seed
                && Distribution == other.Distribution
                && ModifiedBetaShape == other.ModifiedBetaShape
                && ValidIterations == other.ValidIterations
                && FailedIterations == other.FailedIterations
                && PlannedFinishDays == other.PlannedFinishDays
                && PlannedFinishDate == other.PlannedFinishDate
                && MeanDays == other.MeanDays
                && StandardDeviationDays == other.StandardDeviationDays
                && EarliestFinishDays == other.EarliestFinishDays
                && EarliestFinishDate == other.EarliestFinishDate
                && LatestFinishDays == other.LatestFinishDays
                && LatestFinishDate == other.LatestFinishDate
                && Percentiles.SequenceEqual(other.Percentiles)
                && TargetFinishDays == other.TargetFinishDays
                && TargetFinishDate == other.TargetFinishDate
                && TargetProbability == other.TargetProbability
                && Statistics.SequenceEqual(other.Statistics);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Iterations);
            hash.Add(Seed);
            hash.Add(Distribution);
            hash.Add(ModifiedBetaShape);
            hash.Add(ValidIterations);
            hash.Add(FailedIterations);
            hash.Add(PlannedFinishDays);
            hash.Add(MeanDays);
            hash.Add(StandardDeviationDays);
            hash.Add(TargetFinishDays);
            hash.Add(TargetProbability);

            foreach (JobSimulationPercentile percentile in Percentiles)
            {
                hash.Add(percentile);
            }

            foreach (SimulationStatisticModel statistic in Statistics)
            {
                hash.Add(statistic);
            }

            return hash.ToHashCode();
        }

        // The metrics of the run the view-model has just made, which was asked for as the request says.
        internal static JobSimulationMetrics From(
            IScheduleSimulationManagerViewModel simulation,
            SimulationRequest request)
        {
            ArgumentNullException.ThrowIfNull(simulation);
            ArgumentNullException.ThrowIfNull(request);

            SimulationSummaryModel summary = simulation.Summary ?? throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_SimulationNoResult);
            bool hasResult = summary.HasResult;

            List<SimulationStatisticModel> statistics =
            [
                new SimulationStatisticModel { Name = Resource.ProjectPlan.Labels.Label_SimulationSeed, Value = request.Seed.ToString(CultureInfo.CurrentCulture) },
                new SimulationStatisticModel { Name = Resource.ProjectPlan.Labels.Label_SimulationDistribution, Value = StringConverters.DurationDistributionValue(request.Distribution) },
                .. simulation.Statistics,
            ];

            return new JobSimulationMetrics
            {
                Iterations = summary.IterationCount,
                Seed = request.Seed,
                Distribution = request.Distribution,
                ModifiedBetaShape = request.Distribution == DurationDistribution.ModifiedBetaPert ? request.ModifiedBetaShape : null,
                ValidIterations = summary.ValidIterationCount,
                FailedIterations = summary.FailedIterationCount,
                PlannedFinishDays = summary.PlannedFinishTime,
                PlannedFinishDate = simulation.GetFinishDate(summary.PlannedFinishTime),
                MeanDays = hasResult ? summary.Mean : null,
                StandardDeviationDays = hasResult ? summary.StandardDeviation : null,
                EarliestFinishDays = hasResult ? summary.Minimum : null,
                EarliestFinishDate = hasResult ? simulation.GetFinishDate(summary.Minimum) : null,
                LatestFinishDays = hasResult ? summary.Maximum : null,
                LatestFinishDate = hasResult ? simulation.GetFinishDate(summary.Maximum) : null,
                Percentiles = [.. summary.Percentiles.Select(x => new JobSimulationPercentile(x.Percentile, x.FinishTime, simulation.GetFinishDate(x.FinishTime)))],
                TargetFinishDays = hasResult ? summary.TargetFinishTime : null,
                TargetFinishDate = hasResult && summary.TargetFinishTime is int target ? simulation.GetFinishDate(target) : null,
                TargetProbability = hasResult ? summary.TargetProbability : null,
                Statistics = statistics,
            };
        }
    }

    // The finish time that at least the given share of the valid iterations reach (the nearest rank).
    public record JobSimulationPercentile(
        int Percentile,
        int FinishDays,
        DateOnly FinishDate);
}
