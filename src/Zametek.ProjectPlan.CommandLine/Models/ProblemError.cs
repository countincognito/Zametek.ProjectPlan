using System.Text.Json.Serialization;

namespace Zametek.ProjectPlan.CommandLine
{
    // One thing wrong with a request, in a problem's errors: where it is - a JSON pointer, in URI-fragment form, into the
    // request as OpenAPI models a multipart body, which is an object whose properties are its parts (#/options/outputs/ev,
    // say), or the query parameter - a code for the rule it breaks, and what is wrong, for people: a phrase that reads on
    // from the place ("must be from 1 to 5000 pixels").
    public record ProblemError
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Pointer { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Parameter { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Code { get; init; }

        public required string Detail { get; init; }
    }
}
