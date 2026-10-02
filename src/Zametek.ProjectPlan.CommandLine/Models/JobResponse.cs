using System.Text.Json;

namespace Zametek.ProjectPlan.CommandLine
{
    // What zpp serve answers a job with: the exit code zpp would have ended with, and the text it would have printed -
    // rendered on the server, so that the server's culture writes it, whatever the caller's - with the metrics as zpp
    // writes them with --metrics-format json, when the plan compiled, and the outputs the job produced, in the order it
    // produced them.
    public record JobResponse(
        string JobId,
        int ExitCode,
        string Stdout,
        string Stderr,
        JsonElement? Metrics,
        IReadOnlyList<JobResponseOutput> Outputs);
}
