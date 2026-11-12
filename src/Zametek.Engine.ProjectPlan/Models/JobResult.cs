using Zametek.Common.ProjectPlan;

namespace Zametek.Engine.ProjectPlan
{
    // How a job ended. The outputs themselves went to the job's sink as they were produced.
    public record JobResult
    {
        public JobStatus Status { get; init; }

        // What the compiler reported, when the plan did not compile, as zpp prints it.
        public string CompilationOutput { get; init; } = string.Empty;

        // The same report, one error at a time.
        public IReadOnlyList<JobCompilationError> CompilationErrors { get; init; } = [];

        // The compiled plan's metrics, taken once every output had been produced. A plan that did not compile has none.
        public JobMetrics? Metrics { get; init; }

        // What the simulation found, when the job ran one and it gave a result: the request asked for it, and it did not
        // fail.
        public JobSimulationMetrics? Simulation { get; init; }

        // The tracked scenarios of the project, in the order of the project, each with its metrics, when the request asked to
        // compile them (CompileTracked). The scenario that the job loaded has the metrics above when it is tracked.
        public IReadOnlyList<JobTrackedScenario>? TrackedScenarios { get; init; }

        // The data of the scenario chart, from the settings of the chart that the project keeps, when the job compiled the
        // tracked scenarios (CompileTracked, or the scenario chart). Null when the chart has no metric for the axis of X, or for
        // both axes of Y.
        public ScenarioChartDataModel? ScenarioChart { get; init; }

        // Two results are equal when they say the same thing: a record would compare the list of errors by reference,
        // not by what is in it.
        public virtual bool Equals(JobResult? other)
        {
            return other is not null
                && Status == other.Status
                && CompilationOutput == other.CompilationOutput
                && CompilationErrors.SequenceEqual(other.CompilationErrors)
                && Equals(Metrics, other.Metrics)
                && Equals(Simulation, other.Simulation)
                && Equals(ScenarioChart, other.ScenarioChart)
                && (TrackedScenarios is null
                    ? other.TrackedScenarios is null
                    : other.TrackedScenarios is not null && TrackedScenarios.SequenceEqual(other.TrackedScenarios));
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Status);
            hash.Add(CompilationOutput);
            hash.Add(Metrics);
            hash.Add(Simulation);
            hash.Add(ScenarioChart);

            foreach (JobTrackedScenario tracked in TrackedScenarios ?? [])
            {
                hash.Add(tracked);
            }

            foreach (JobCompilationError error in CompilationErrors)
            {
                hash.Add(error);
            }

            return hash.ToHashCode();
        }
    }
}
