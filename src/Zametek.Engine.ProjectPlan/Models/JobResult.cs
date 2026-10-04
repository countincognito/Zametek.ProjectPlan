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

        // Two results are equal when they say the same thing: a record would compare the list of errors by reference,
        // not by what is in it.
        public virtual bool Equals(JobResult? other)
        {
            return other is not null
                && Status == other.Status
                && CompilationOutput == other.CompilationOutput
                && CompilationErrors.SequenceEqual(other.CompilationErrors)
                && Equals(Metrics, other.Metrics);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Status);
            hash.Add(CompilationOutput);
            hash.Add(Metrics);

            foreach (JobCompilationError error in CompilationErrors)
            {
                hash.Add(error);
            }

            return hash.ToHashCode();
        }
    }
}
