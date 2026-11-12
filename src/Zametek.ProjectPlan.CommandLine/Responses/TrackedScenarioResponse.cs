using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // A tracked scenario of the project, and the metrics that the job compiled for it: its path (the names of the folders
    // that it is in, and then its own), its id, and whether it is the scenario that the job loaded. A scenario that did not
    // compile, or that the project has no data for, has null metrics.
    public record TrackedScenarioResponse(
        string Path,
        Guid Id,
        bool IsCurrent,
        MetricsResponse? Metrics)
    {
        public static IReadOnlyList<TrackedScenarioResponse>? From(IReadOnlyList<JobTrackedScenario>? trackedScenarios)
        {
            return trackedScenarios is null
                ? null
                : [.. trackedScenarios.Select(x => new TrackedScenarioResponse(x.Path, x.Id, x.IsCurrent, x.Metrics is null ? null : MetricsResponse.From(x.Metrics)))];
        }
    }
}
