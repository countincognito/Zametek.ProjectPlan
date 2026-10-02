namespace Zametek.ProjectPlan.CommandLine
{
    // How a run of zpp ended. The values are part of the CLI contract: scripts and CI gates branch on them.
    public enum ExitCode
    {
        // The run did everything it was asked to.
        Success = 0,

        // A runtime failure: bad paths, unreadable files, outputs that could not be written, unexpected errors.
        Failure = 1,

        // Bad usage: invalid options or combinations.
        UsageError = 2,

        // The project compiled with errors - kept distinct from Failure so a pipeline can tell a broken plan from a
        // broken invocation.
        CompilationErrors = 3,

        // A compilation ran past --compile-timeout and was cancelled, which says nothing about whether the plan is
        // valid, only that it did not finish.
        CompilationTimeout = 4,

        // The server the job was sent to did not run it: it could not be reached, refused the request - for want of
        // its API key, or because the job went beyond its limits - stayed busy, stopped the job at its time limit, or
        // answered with something zpp cannot read. Kept distinct from Failure so that a pipeline can tell a server it
        // could try again, or do without, from a job that failed.
        ServerFailure = 5,
    }
}
