namespace Zametek.Common.ProjectPlan
{
    /// <summary>
    /// The CSV files that a simulation can write. Each is a view of the same result, and a caller can ask
    /// for any of them together.
    /// </summary>
    [Flags]
    public enum SimulationCsvFile
    {
        None = 0,

        /// <summary>
        /// The statistics of the finish time.
        /// </summary>
        Statistics = 1 << 0,

        /// <summary>
        /// The histogram of the finish times, split by critical path.
        /// </summary>
        Histogram = 1 << 1,

        /// <summary>
        /// The critical paths.
        /// </summary>
        Paths = 1 << 2,

        /// <summary>
        /// How often each activity was critical.
        /// </summary>
        Activities = 1 << 3,

        /// <summary>
        /// Every iteration, with its finish time and its critical paths.
        /// </summary>
        Iterations = 1 << 4,

        All = Statistics | Histogram | Paths | Activities | Iterations,
    }
}
