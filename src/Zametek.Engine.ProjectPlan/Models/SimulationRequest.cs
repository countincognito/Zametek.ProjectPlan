using Zametek.Maths.Graphs;

namespace Zametek.Engine.ProjectPlan
{
    // A Monte Carlo simulation of the schedule, run on the plan once it has compiled: the same settings as the panel of the
    // desktop has. A simulation with the same settings, on the same plan, gives the same result on every run and on every
    // number of threads. Its statistics go into the job's result, and its chart and CSV files are outputs of the job (see
    // JobRequest).
    public record SimulationRequest
    {
        // The number of iterations a simulation has when the request does not give one.
        public const int DefaultIterations = SimulationSettings.DefaultIterations;

        // The seed a simulation has when the request does not give one: a fixed seed, so that a run repeats.
        public const ulong DefaultSeed = 1;

        // The shape value that the modified beta-PERT distribution has when the request does not give one: the usual 4.
        public const double DefaultModifiedBetaShape = 4.0;

        // From 1 to GraphLimits.MaximumSimulationIterations.
        public int Iterations { get; init; } = DefaultIterations;

        public ulong Seed { get; init; } = DefaultSeed;

        public DurationDistribution Distribution { get; init; } = DurationDistribution.BetaPert;

        // Only the modified beta-PERT distribution uses it. It must be above 0.
        public double ModifiedBetaShape { get; init; } = DefaultModifiedBetaShape;

        // The finish time, in days from the start of the project, whose chance of being met the statistics give.
        public int? TargetFinishTime { get; init; }
    }
}
