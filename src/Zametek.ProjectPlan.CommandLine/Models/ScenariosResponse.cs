using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // What zpp serve answers a request for a project's scenarios with: the exit code and text of zpp --list-scenarios,
    // and the scenarios themselves, when the project could be read. The transcript says what was printed, call by call,
    // as a job's does.
    public record ScenariosResponse(
        string JobId,
        int ExitCode,
        string Stdout,
        string Stderr,
        IReadOnlyList<ScenarioSummary>? Scenarios,
        IReadOnlyList<JobTranscriptEntry> Transcript);
}
