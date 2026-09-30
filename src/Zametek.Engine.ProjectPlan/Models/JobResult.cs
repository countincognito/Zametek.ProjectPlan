namespace Zametek.Engine.ProjectPlan
{
    // How a job ended. The outputs themselves went to the job's sink as they were produced.
    public record JobResult
    {
        public JobStatus Status { get; init; }

        // What the compiler reported, when the plan did not compile.
        public string CompilationOutput { get; init; } = string.Empty;

        // The compiled plan's metrics, taken once every output had been produced. A plan that did not compile has none.
        public JobMetrics? Metrics { get; init; }
    }
}
