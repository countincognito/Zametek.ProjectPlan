namespace Zametek.Engine.ProjectPlan
{
    // A tracked scenario of a project, and the metrics that a job compiled for it. The path is the name of the scenario,
    // after the names of the folders that it is in, as a listing of the scenarios shows it. A scenario that did not compile,
    // or that the file has no data for, has no metrics.
    public record JobTrackedScenario(
        string Path,
        Guid Id,
        bool IsCurrent,
        JobMetrics? Metrics);
}
