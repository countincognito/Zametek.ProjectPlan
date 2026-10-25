using System.Text.Json.Serialization;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // What zpp serve answers a request for a project's scenarios with: the scenarios, as zpp --list-scenarios lists them.
    // The console is there when the request asked for it.
    public record ScenariosResponse(
        IReadOnlyList<ScenarioSummary> Scenarios,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ConsoleResponse? Console);
}
