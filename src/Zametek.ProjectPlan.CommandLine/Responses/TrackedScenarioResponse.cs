using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // A tracked scenario of the project, and the metrics that the job compiled for it: its path (the names of the folders
    // that it is in, and then its own), its id, and whether it is the scenario that the job loaded. A scenario that did not
    // compile, or that the project has no data for, has null metrics. Errors says why a scenario did not compile, and it is
    // empty for any other.
    public record TrackedScenarioResponse(
        string Path,
        Guid Id,
        bool IsCurrent,
        MetricsResponse? Metrics,
        IReadOnlyList<TrackedScenarioErrorResponse> Errors)
    {
        public static IReadOnlyList<TrackedScenarioResponse>? From(IReadOnlyList<JobTrackedScenario>? trackedScenarios)
        {
            return trackedScenarios is null
                ? null
                : [.. trackedScenarios.Select(x => new TrackedScenarioResponse(
                    x.Path,
                    x.Id,
                    x.IsCurrent,
                    x.Metrics is null ? null : MetricsResponse.From(x.Metrics),
                    [.. x.Errors.Select(error => new TrackedScenarioErrorResponse(error.Code, error.Message))]))];
        }
    }

    // One reason why a tracked scenario did not compile: the code of the compiler (P0010, say), or the code of the check of the
    // project before the load, and what is wrong.
    public record TrackedScenarioErrorResponse(
        string Code,
        string Detail);
}
