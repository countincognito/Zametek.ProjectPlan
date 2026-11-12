using System.Globalization;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.Engine.ProjectPlan
{
    // What a simulation found, copied out of the simulation view-model before the job's scope - and the view-model with it -
    // goes: the settings it ran with, and the statistics of the finish time. Every time is in days from the start of the
    // project, with the date it falls on in the working calendar of the project. A failed iteration is in none of the
    // statistics, and the metrics say why the iterations failed. A run in which every iteration failed has no statistics, and
    // so no values for them.
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

        // Why the iterations failed: the resource scheduler could not place every activity, the schedule ran past the limit of
        // time of the library, or an activity could not finish by its maximum latest finish time. A result that a project held
        // from before the reasons were kept has zeros.
        public int ResourceSchedulingStallIterations { get; init; }

        public int MaximumTimeValueExceededIterations { get; init; }

        public int InvalidConstraintsIterations { get; init; }

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

        // Each activity that could not finish by its maximum latest finish time in an iteration, the most misses first.
        public IReadOnlyList<JobSimulationMissedMaximum> MissedMaximumLatestFinishTimes { get; init; } = [];

        // The statistics as the panel shows them, as text: the seed, the distribution and, for a modified beta-PERT, its shape
        // value, then each of the panel's other lines.
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
                && ResourceSchedulingStallIterations == other.ResourceSchedulingStallIterations
                && MaximumTimeValueExceededIterations == other.MaximumTimeValueExceededIterations
                && InvalidConstraintsIterations == other.InvalidConstraintsIterations
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
                && MissedMaximumLatestFinishTimes.SequenceEqual(other.MissedMaximumLatestFinishTimes)
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
            hash.Add(InvalidConstraintsIterations);
            hash.Add(PlannedFinishDays);
            hash.Add(MeanDays);
            hash.Add(StandardDeviationDays);
            hash.Add(TargetFinishDays);
            hash.Add(TargetProbability);

            foreach (JobSimulationPercentile percentile in Percentiles)
            {
                hash.Add(percentile);
            }

            foreach (JobSimulationMissedMaximum missed in MissedMaximumLatestFinishTimes)
            {
                hash.Add(missed);
            }

            foreach (SimulationStatisticModel statistic in Statistics)
            {
                hash.Add(statistic);
            }

            return hash.ToHashCode();
        }

        // The metrics of the result the view-model has, which a run of the job made or the scenario held. The result says
        // which seed, distribution and shape value made it, and so the metrics do not use the settings of the scenario: a
        // person can change those after the run, and then they are not the values of the result.
        internal static JobSimulationMetrics From(ISimulationChartManagerViewModel simulation)
        {
            ArgumentNullException.ThrowIfNull(simulation);

            SimulationSummaryModel summary = simulation.Summary ?? throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_SimulationNoResult);
            bool hasResult = summary.HasResult;

            return new JobSimulationMetrics
            {
                Iterations = summary.IterationCount,
                Seed = summary.Seed,
                Distribution = summary.Distribution,
                ModifiedBetaShape = summary.ModifiedBetaShape,
                ValidIterations = summary.ValidIterationCount,
                FailedIterations = summary.FailedIterationCount,
                ResourceSchedulingStallIterations = summary.ResourceSchedulingStallIterationCount,
                MaximumTimeValueExceededIterations = summary.MaximumTimeValueExceededIterationCount,
                InvalidConstraintsIterations = summary.InvalidConstraintsIterationCount,
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
                MissedMaximumLatestFinishTimes =
                [
                    .. summary.Activities
                        .Where(x => x.MaximumLatestFinishTimeMissCount > 0)
                        .OrderByDescending(x => x.MaximumLatestFinishTimeMissCount)
                        .ThenBy(x => x.ActivityId)
                        .Select(x => new JobSimulationMissedMaximum(x.ActivityId, x.Name, x.MaximumLatestFinishTimeMissCount)),
                ],
                Statistics = [.. simulation.Statistics],
            };
        }
    }

    // The finish time that at least the given share of the valid iterations reach (the nearest rank).
    public record JobSimulationPercentile(
        int Percentile,
        int FinishDays,
        DateOnly FinishDate);

    // An activity that could not finish by its maximum latest finish time, and the number of iterations in which it could not.
    // Each of those iterations failed.
    public record JobSimulationMissedMaximum(
        int ActivityId,
        string Name,
        int Iterations);
}
