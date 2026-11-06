using System.Text.Json.Serialization;

namespace Zametek.ProjectPlan.CommandLine
{
    // What zpp serve answers a request to compile a project with, when it produced everything the request asked for: the
    // project's metrics, what the simulation found - when the request asked for one - and the outputs, in the order the job
    // produced them - as JSON, or in a zip, which holds each output as a file and everything else in result.json. A project
    // that did not compile, or an output that could not be produced, is a problem and not an answer. The console is there
    // when the request asked for it.
    public record CompileResponse(
        MetricsResponse Metrics,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SimulationResponse? Simulation,
        IReadOnlyList<OutputResponse> Outputs,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ConsoleResponse? Console);
}
