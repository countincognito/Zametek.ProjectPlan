using System.Text.Json.Serialization;

namespace Zametek.ProjectPlan.CommandLine
{
    // What zpp serve answers a request to compile a project with, when it produced everything the request asked for: the
    // project's metrics, what the simulation found - when the request asked for one - the metrics of each tracked scenario -
    // when the request asked to compile them - the data of the scenario chart - when the job compiled the tracked scenarios and
    // the chart has axes - and the outputs, in the order the job produced them - as JSON, or in a zip,
    // which holds each output as a file and everything else in result.json. A project that did not compile, or an output
    // that could not be produced, is a problem and not an answer. The console is there when the request asked for it.
    public record CompileResponse(
        MetricsResponse Metrics,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SimulationResponse? Simulation,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<TrackedScenarioResponse>? TrackedScenarios,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ScenarioChartResponse? ScenarioChart,
        IReadOnlyList<OutputResponse> Outputs,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ProblemError>? Warnings,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ConsoleResponse? Console);
}
