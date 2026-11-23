namespace Zametek.ProjectPlan.CommandLine
{
    // The limits zpp serve works within. Each has a default here, which configuration overrides: first zpp-serve.json,
    // if there is one beside zpp, then an environment variable named for the limit after ZPP_ (ZPP_MaxJobs, for one),
    // then the option of zpp serve's for it - each overriding the one before.
    public record ServeLimits
    {
        // The longest time of a job (JobTimeoutSeconds), in whole seconds. The time limit of a job is a timer, and a timer of
        // .NET waits for 4,294,967,294 milliseconds at most, about 49 days.
        public const int MaximumJobTimeoutSeconds = (int)((uint.MaxValue - 1) / 1000);

        // How many jobs run at once.
        public int MaxJobs { get; init; } = Environment.ProcessorCount;

        // How many jobs may wait for one to finish. Any more are turned away, with 503 and Retry-After. When it is not
        // configured, it is twice MaxJobs.
        public int MaxQueue { get; init; } = 2 * Environment.ProcessorCount;

        // The fewest connections that the server keeps open at once when MaxConnections is not configured.
        public const int MinimumDefaultConnections = 100;

        // The connections for the probes and for the paths that need no key, in the default of MaxConnections.
        public const int ConnectionsForProbes = 10;

        // How many connections the server keeps open at once. The web server closes a connection above the limit at once,
        // with no answer. The paths that need no key (the description of the API, the schema and the health checks) are
        // outside the limiter of the jobs, and so this limit is the only one that bounds their requests. When it is not
        // configured, it is DefaultMaxConnections of MaxJobs and MaxQueue.
        public int MaxConnections { get; init; } = DefaultMaxConnections(Environment.ProcessorCount, 2 * Environment.ProcessorCount);

        // 100, or more when the jobs need more: a connection for each job that runs and one for a request of its status, a
        // connection for each job that waits, and ConnectionsForProbes. A connection above the limit gets no answer, and so a
        // limit at or below MaxJobs and MaxQueue together closed the connection of a request that waited for a place, and no
        // client got the 503 with Retry-After of a full queue. The default of 100 did this on a computer with 34 processors or
        // more (from the review of the branch).
        public static int DefaultMaxConnections(int maxJobs, int maxQueue) =>
            (int)Math.Clamp((2L * maxJobs) + maxQueue + ConnectionsForProbes, MinimumDefaultConnections, int.MaxValue);

        // The largest request, in megabytes of 1,048,576 bytes: the plan, its options, and the framing around them.
        public int MaxUploadMegabytes { get; init; } = 50;

        // The largest chart, in pixels.
        public int MaxChartWidth { get; init; } = 5000;

        public int MaxChartHeight { get; init; } = 5000;

        // How long a job may run. A job that runs past it stops before its next step, as a cancelled job does.
        public int JobTimeoutSeconds { get; init; } = 120;

        // The most iterations a simulation may be asked to run. Each schedules the resources again, which costs about as much
        // as a compilation, and so it is also the job's time limit (JobTimeoutSeconds) that stops a simulation that is too long.
        public int MaxSimulationIterations { get; init; } = 100_000;

        // The longest a job may let its compilation run. Unlike zpp's, a job's compilation cannot run without a limit.
        public int MaxCompileTimeoutMilliseconds { get; init; } = 60_000;
    }
}
