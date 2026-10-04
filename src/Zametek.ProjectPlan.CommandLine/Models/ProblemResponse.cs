using System.Text.Json.Serialization;

namespace Zametek.ProjectPlan.CommandLine
{
    // A problem, as RFC 9457 has it and zpp serve writes it, as application/problem+json: the type that says which kind of
    // problem it is, the title that is the same for every problem of its type, the status, what is wrong this time, and the
    // trace id of the request - which is also its Request-Id header - to find it in the server's log by. Errors lists every
    // problem a request's content has. The members after it are extensions, which only some types have: the console, which
    // a request asked for, and for a project that compiled but could not produce an output, the metrics and the outputs
    // that were produced.
    public record ProblemResponse
    {
        public required string Type { get; init; }

        public required string Title { get; init; }

        public required int Status { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Detail { get; init; }

        public required string TraceId { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<ProblemError>? Errors { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MetricsResponse? Metrics { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<OutputResponse>? Outputs { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ConsoleResponse? Console { get; init; }
    }
}
