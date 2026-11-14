namespace Zametek.Engine.ProjectPlan
{
    // A tracked scenario of a project, and the metrics that a job compiled for it. The path is the name of the scenario,
    // after the names of the folders that it is in, as a listing of the scenarios shows it. A scenario that did not compile,
    // or that the file has no data for, has no metrics. Errors says why a scenario did not compile: the errors of the
    // compiler, or the problems that the check before the load found. It is empty for a scenario that compiled, and for one
    // that the file has no data for.
    public record JobTrackedScenario(
        string Path,
        Guid Id,
        bool IsCurrent,
        JobMetrics? Metrics)
    {
        public IReadOnlyList<JobCompilationError> Errors { get; init; } = [];

        // Two tracked scenarios are equal when they say the same thing: a record would compare the list of errors by
        // reference, not by what is in it.
        public virtual bool Equals(JobTrackedScenario? other)
        {
            return other is not null
                && Path == other.Path
                && Id == other.Id
                && IsCurrent == other.IsCurrent
                && Equals(Metrics, other.Metrics)
                && Errors.SequenceEqual(other.Errors);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Path);
            hash.Add(Id);
            hash.Add(IsCurrent);
            hash.Add(Metrics);

            foreach (JobCompilationError error in Errors)
            {
                hash.Add(error);
            }

            return hash.ToHashCode();
        }
    }
}
