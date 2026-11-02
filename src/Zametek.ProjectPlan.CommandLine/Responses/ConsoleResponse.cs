namespace Zametek.ProjectPlan.CommandLine
{
    // What zpp would have printed and exited with for a request: the exit code, the text on each of its two streams -
    // written on the server, so that the server's culture writes it, whatever the caller's - and the transcript, which says
    // what was printed and produced, call by call, in the order it was, for a client that prints and writes them as zpp
    // would have. A request asks for it with include=console, and it comes with a problem as well as with an answer.
    public record ConsoleResponse(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        IReadOnlyList<JobTranscriptEntry> Transcript);
}
