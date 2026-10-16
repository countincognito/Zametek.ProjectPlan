using System.Text.Json.Serialization;
using Zametek.Engine.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ProjectPlan.CommandLine
{
    // A simulation a request asks for: zpp's --simulate and --simulation-* options. The simulation runs once the project has
    // compiled, and its statistics are in the answer. The outputs of a simulation are asked for beside it (see
    // OutputsOptions), and need it.
    public record SimulationOptions
    {
        // --simulation-iterations: from 1 to the server's limit.
        public int Iterations { get; init; } = SimulationRequest.DefaultIterations;

        // --simulation-seed: a whole number from 0 to 18,446,744,073,709,551,615, which the API writes as text, because a
        // number that large is not one that every program reads without loss. The same seed gives the same result.
        // A seed of 0 is a seed, and so it is always written: the client leaves out what is default, and 0 is not the default seed.
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
        public ulong Seed { get; init; } = SimulationRequest.DefaultSeed;

        // --simulation-distribution
        public DurationDistribution Distribution { get; init; } = DurationDistribution.BetaPert;

        // --simulation-shape: a number above 0. Only the modified beta-PERT distribution takes it, and it has 4 when this
        // is not given.
        public double? ModifiedBetaShape { get; init; }

        // --simulation-target: the finish, in days from the start of the project, whose chance of being met the answer gives.
        public int? TargetFinishDays { get; init; }
    }
}
