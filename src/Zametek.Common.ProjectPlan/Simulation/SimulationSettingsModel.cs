using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
{
    /// <summary>
    /// The settings of the schedule simulation of a scenario. They are part of the scenario, as its display
    /// settings are: a project file holds them, and so does the Excel export.
    /// </summary>
    [Serializable]
    public record SimulationSettingsModel
    {
        /// <summary>
        /// The shape value that the modified beta-PERT distribution has when the user does not change it.
        /// </summary>
        public const double DefaultModifiedBetaShape = 4.0;

        /// <summary>
        /// The seed that a scenario has when nobody gives another.
        /// </summary>
        public const ulong DefaultSeed = 1;

        /// <summary>
        /// The number of iterations of a run. A run accepts the numbers from 1 to the limit of the library.
        /// </summary>
        public int Iterations { get; init; } = SimulationSettings.DefaultIterations;

        public DurationDistribution Distribution { get; init; } = DurationDistribution.BetaPert;

        /// <summary>
        /// Only the modified beta-PERT distribution uses it.
        /// </summary>
        public double ModifiedBetaShape { get; init; } = DefaultModifiedBetaShape;

        /// <summary>
        /// The seed of the random numbers. After a run, it is the seed that the run used.
        /// </summary>
        public ulong Seed { get; init; } = DefaultSeed;

        /// <summary>
        /// Whether each run of the panel draws a new seed. A run of <c>zpp</c> never does.
        /// </summary>
        public bool UseRandomSeed { get; init; } = true;

        /// <summary>
        /// The finish time, in days from the start of the project, whose chance of being met the statistics
        /// give, or null.
        /// </summary>
        public int? TargetFinishTime { get; init; }
    }
}
