using System.Text.Json.Serialization;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // One output of a request: which it is, the name zpp would give its file, its media type, and its content - which is
    // left out of a zip's result.json, as the zip holds the file itself.
    public record OutputResponse(
        JobOutput Kind,
        string FileName,
        string ContentType,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] byte[]? Content);
}
