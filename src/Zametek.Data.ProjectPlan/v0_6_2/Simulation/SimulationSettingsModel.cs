using Zametek.Maths.Graphs;

namespace Zametek.Data.ProjectPlan.v0_6_2
{
    // The settings of the schedule simulation of a scenario. They are new in v0.6.2. A scenario that an older file holds,
    // or a v0.6.2 file from before there were settings, has these defaults, and they are the defaults of the Current model
    // (the model of a version does not change, and so the values are written here and not read from there).
    [Serializable]
    public record SimulationSettingsModel
    {
        public int Iterations { get; init; } = 5000;

        public DurationDistribution Distribution { get; init; } = DurationDistribution.BetaPert;

        public double ModifiedBetaShape { get; init; } = 4.0;

        public ulong Seed { get; init; } = 1;

        public bool UseRandomSeed { get; init; } = true;

        public int? TargetFinishTime { get; init; }
    }
}
