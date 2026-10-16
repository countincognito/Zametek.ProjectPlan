using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
{
    /// <summary>
    /// A copy of the plan, taken at one moment, that a simulation runs on. The simulation never touches
    /// the live activities: it changes the durations of the copies, and it takes a long time.
    /// </summary>
    public record SimulationInputModel
    {
        /// <summary>
        /// The activities, in the order of the plan, as a compile takes them. Each has its expected
        /// duration as its duration.
        /// </summary>
        public IReadOnlyList<IDependentActivity<int, int, int>> Activities { get; init; } = [];

        public IReadOnlyList<IResource<int, int>> Resources { get; init; } = [];

        public IReadOnlyList<IWorkStream<int>> WorkStreams { get; init; } = [];

        /// <summary>
        /// The three estimates of each activity, by the ID of the activity.
        /// </summary>
        public IReadOnlyDictionary<int, DurationEstimate> Estimates { get; init; } = new Dictionary<int, DurationEstimate>();

        /// <summary>
        /// The name of each activity, by its ID.
        /// </summary>
        public IReadOnlyDictionary<int, string> ActivityNames { get; init; } = new Dictionary<int, string>();

        /// <summary>
        /// The revision of the compilation outputs when the copy was taken. A result that has an older
        /// revision than the plan does not belong to it any more.
        /// </summary>
        public int CompilationOutputRevision { get; init; }
    }
}
