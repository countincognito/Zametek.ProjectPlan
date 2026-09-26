namespace Zametek.ProjectPlan.Engine
{
    // One of a project's scenarios, as a listing shows it. The path is the scenario's name, prefixed with the names of
    // the folders it sits in, so that scenarios of the same name in different folders stay distinguishable.
    public record ScenarioSummary(
        string Path,
        Guid Id,
        bool IsTracked,
        bool IsCurrent);
}
