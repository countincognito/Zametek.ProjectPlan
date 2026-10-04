namespace Zametek.ProjectPlan.CommandLine
{
    // The kinds of problem zpp serve answers with where the status says too little: each has a type of its own - documented
    // in docs/API.md under its slug - and a status that never changes with it. The other problems a request can meet - a
    // missing API key, an unknown path, a body that is too large - are what their status says, and have no kind.
    public enum ProblemKind
    {
        // The request cannot be understood: its parts or its options are not readable, or a parameter is not one it takes.
        MalformedRequest,

        // The request is understood, and what it asks for or says is not valid. Every problem is listed.
        ValidationFailed,

        // The file sent as the project, or as the workbook to import, is not one the server can read.
        ProjectNotReadable,

        // The project has compilation errors. Each is listed.
        CompilationFailed,

        // The compilation did not finish within its time limit.
        CompilationTimedOut,

        // The scenario the request named cannot be selected: it is not there, it names several, or it holds no data.
        ScenarioNotSelectable,

        // The project compiled, and an output it was asked for could not be produced. The outputs that were come with it.
        OutputFailed,

        // Anything the server did not expect: its cause is in its log, under the request's trace id, and not in the answer.
        UnexpectedError,

        // All the jobs the server runs at once are running, and as many wait as it allows.
        Busy,

        // The job ran for longer than the server allows jobs to, and was stopped.
        JobTimeout,
    }
}
