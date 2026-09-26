namespace Zametek.ProjectPlan.Engine
{
    // How a job ended, when it ran to its end. A job that could not - its input unreadable, its scenario not found,
    // its compilation out of time, or its project or scenario export not stored - throws instead.
    public enum JobStatus
    {
        // The plan compiled, and every output asked for was produced.
        Succeeded,

        // The plan compiled and the job ran to its end, but it reported an error on the way - typically a chart or
        // graph it could not produce, while the others were produced - so as a whole it failed.
        CompletedWithErrors,

        // The plan did not compile, so nothing was produced. JobResult.CompilationOutput says why.
        CompilationErrors
    }
}
