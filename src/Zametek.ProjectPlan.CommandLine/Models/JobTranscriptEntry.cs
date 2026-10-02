using System.Text.Json.Serialization;

namespace Zametek.ProjectPlan.CommandLine
{
    // One thing a job did, in the order it did it: printed a line or a block on stdout, or a line on stderr - with the
    // text it printed, every line end in it a \n - or produced an output. Replayed in order through zpp's own console
    // and file sink, a job's transcript prints and writes what zpp would have, in the order zpp would have, and a block
    // that reports errors shows as zpp shows one.
    public record JobTranscriptEntry
    {
        public required JobTranscriptKind Kind { get; init; }

        // What a line or a block printed.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Text { get; init; }

        // Whether a block reports errors.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool HasErrors { get; init; }

        // Which output the job produced: its index in the response's outputs.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Index { get; init; }
    }
}
