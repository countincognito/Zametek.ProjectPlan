using System.Text.Json.Serialization;
using Zametek.Engine.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ProjectPlan.CommandLine
{
    // What a simulation found, as zpp serve's API gives it: the settings it ran with, and the statistics of the finish. Like
    // the metrics, they are values and not the text zpp prints, named in words, and nothing here is written in the
    // server's culture. Every time is in days from the start of the project, with the date it falls on in the working
    // calendar of the project. A failed iteration is in none of the statistics, and the answer says why the iterations
    // failed. A run in which every iteration failed has no statistics, and so no values for them.
    public record SimulationResponse
    {
        // The iterations that were asked for. ValidIterations of them gave a finish; the others - FailedIterations - did not,
        // and are in none of the statistics.
        public int Iterations { get; init; }

        public int ValidIterations { get; init; }

        public int FailedIterations { get; init; }

        // Why the iterations failed: the resource scheduler could not place every activity, the schedule ran past the limit of
        // time, or an activity could not finish by its maximum latest finish time.
        public int ResourceSchedulingStallIterations { get; init; }

        public int MaximumTimeValueExceededIterations { get; init; }

        public int InvalidConstraintsIterations { get; init; }

        // Written as text (see SimulationOptions).
        [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
        public ulong Seed { get; init; }

        public DurationDistribution Distribution { get; init; }

        // The shape value the run used. Only the modified beta-PERT distribution has one.
        public double? ModifiedBetaShape { get; init; }

        // The finish of the project as it stands, with the expected duration of each activity.
        public int PlannedFinishDays { get; init; }

        public DateOnly PlannedFinishDate { get; init; }

        public double? MeanDays { get; init; }

        public double? StandardDeviationDays { get; init; }

        public int? EarliestFinishDays { get; init; }

        public DateOnly? EarliestFinishDate { get; init; }

        public int? LatestFinishDays { get; init; }

        public DateOnly? LatestFinishDate { get; init; }

        // The finish that at least the percentile's share of the valid iterations reach, for the 5th, 10th, 25th, 50th, 75th,
        // 80th, 90th, 95th and 99th percentiles.
        public IReadOnlyList<SimulationPercentileResponse> Percentiles { get; init; } = [];

        // The target the request gave, and the share of the valid iterations that finish by it, from 0 to 1.
        public int? TargetFinishDays { get; init; }

        public DateOnly? TargetFinishDate { get; init; }

        public double? TargetProbability { get; init; }

        // Each activity that could not finish by its maximum latest finish time in an iteration, the most misses first.
        public IReadOnlyList<SimulationMissedMaximumResponse> MissedMaximumLatestFinishTimes { get; init; } = [];

        public static SimulationResponse From(JobSimulationMetrics simulation)
        {
            ArgumentNullException.ThrowIfNull(simulation);

            return new SimulationResponse
            {
                Iterations = simulation.Iterations,
                ValidIterations = simulation.ValidIterations,
                FailedIterations = simulation.FailedIterations,
                ResourceSchedulingStallIterations = simulation.ResourceSchedulingStallIterations,
                MaximumTimeValueExceededIterations = simulation.MaximumTimeValueExceededIterations,
                InvalidConstraintsIterations = simulation.InvalidConstraintsIterations,
                Seed = simulation.Seed,
                Distribution = simulation.Distribution,
                ModifiedBetaShape = simulation.ModifiedBetaShape,
                PlannedFinishDays = simulation.PlannedFinishDays,
                PlannedFinishDate = simulation.PlannedFinishDate,
                MeanDays = simulation.MeanDays,
                StandardDeviationDays = simulation.StandardDeviationDays,
                EarliestFinishDays = simulation.EarliestFinishDays,
                EarliestFinishDate = simulation.EarliestFinishDate,
                LatestFinishDays = simulation.LatestFinishDays,
                LatestFinishDate = simulation.LatestFinishDate,
                Percentiles = [.. simulation.Percentiles.Select(x => new SimulationPercentileResponse(x.Percentile, x.FinishDays, x.FinishDate))],
                TargetFinishDays = simulation.TargetFinishDays,
                TargetFinishDate = simulation.TargetFinishDate,
                TargetProbability = simulation.TargetProbability,
                MissedMaximumLatestFinishTimes = [.. simulation.MissedMaximumLatestFinishTimes.Select(x => new SimulationMissedMaximumResponse(x.ActivityId, x.Name, x.Iterations))],
            };
        }

        // Two answers are equal when they say the same thing: a record would compare the lists by reference, not by what is
        // in them.
        public virtual bool Equals(SimulationResponse? other)
        {
            return other is not null
                && Iterations == other.Iterations
                && ValidIterations == other.ValidIterations
                && FailedIterations == other.FailedIterations
                && ResourceSchedulingStallIterations == other.ResourceSchedulingStallIterations
                && MaximumTimeValueExceededIterations == other.MaximumTimeValueExceededIterations
                && InvalidConstraintsIterations == other.InvalidConstraintsIterations
                && Seed == other.Seed
                && Distribution == other.Distribution
                && ModifiedBetaShape == other.ModifiedBetaShape
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
                && MissedMaximumLatestFinishTimes.SequenceEqual(other.MissedMaximumLatestFinishTimes);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Iterations);
            hash.Add(FailedIterations);
            hash.Add(Seed);
            hash.Add(Distribution);
            hash.Add(PlannedFinishDays);
            hash.Add(MeanDays);
            hash.Add(TargetFinishDays);
            hash.Add(TargetProbability);

            foreach (SimulationPercentileResponse percentile in Percentiles)
            {
                hash.Add(percentile);
            }

            foreach (SimulationMissedMaximumResponse missed in MissedMaximumLatestFinishTimes)
            {
                hash.Add(missed);
            }

            return hash.ToHashCode();
        }
    }

    public record SimulationPercentileResponse(
        int Percentile,
        int FinishDays,
        DateOnly FinishDate);

    // An activity that could not finish by its maximum latest finish time, and the number of iterations in which it could not.
    public record SimulationMissedMaximumResponse(
        int ActivityId,
        string Name,
        int Iterations);
}
