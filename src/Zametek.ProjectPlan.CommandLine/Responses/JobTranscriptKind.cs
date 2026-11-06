namespace Zametek.ProjectPlan.CommandLine
{
    // What a job did, as its transcript records it: each of the three ways it prints on zpp's console, and producing an
    // output.
    public enum JobTranscriptKind
    {
        // A line on stdout, as IJobConsole.WriteLineAsync prints one.
        Line,

        // A block on stdout, after a blank line, as IJobConsole.DisplayAsync prints one.
        Display,

        // A line on stderr, as IJobConsole.WriteErrorLineAsync prints one.
        ErrorLine,

        // One of the job's outputs, which zpp writes to its file as soon as the job produces it.
        Output,
    }
}
