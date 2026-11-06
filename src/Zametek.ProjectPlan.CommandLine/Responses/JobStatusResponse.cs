namespace Zametek.ProjectPlan.CommandLine
{
    // The state of a job that the server runs now, as GET /v1/jobs/{requestId} gives it. The job is a request, and its id is the
    // id of the request: the trace id that the caller gave it in a traceparent. The server keeps nothing of a job once it has
    // ended, and so a job that is not running has no status, and the answer to its own request is the only word of it.
    public record JobStatusResponse(
        string Id,
        JobState State,
        DateTimeOffset StartedAt,
        TimeSpan Elapsed,
        JobProgressResponse? Progress);

    // How far the job has got in its one step that takes long enough to say: the simulation. It has run Done of its Total
    // iterations. A job that does not simulate, or that has not begun to, has none.
    public record JobProgressResponse(
        int Done,
        int Total);

    // The states a job can be in while the server has a status for it. The guide (ASY-2) has more, which a server that keeps
    // the status of a job after it ends would have.
    public enum JobState
    {
        Running,
    }
}
