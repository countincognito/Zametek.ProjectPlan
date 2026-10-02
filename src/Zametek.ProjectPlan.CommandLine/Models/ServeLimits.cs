namespace Zametek.ProjectPlan.CommandLine
{
    // The limits zpp serve works within. Each has a default here, which configuration overrides: first zpp-serve.json,
    // if there is one beside zpp, then an environment variable named for the limit after ZPP_ (ZPP_MaxJobs, for one),
    // then the option of zpp serve's for it - each overriding the one before.
    public record ServeLimits
    {
        // How many jobs run at once.
        public int MaxJobs { get; init; } = Environment.ProcessorCount;

        // How many jobs may wait for one to finish. Any more are turned away, with 503 and Retry-After. When it is not
        // configured, it is twice MaxJobs.
        public int MaxQueue { get; init; } = 2 * Environment.ProcessorCount;

        // The largest request, in megabytes of 1,048,576 bytes: the plan, its options, and the framing around them.
        public int MaxUploadMegabytes { get; init; } = 50;

        // The largest chart, in pixels.
        public int MaxChartWidth { get; init; } = 5000;

        public int MaxChartHeight { get; init; } = 5000;

        // How long a job may run. A job that runs past it stops before its next step, as a cancelled job does.
        public int JobTimeoutSeconds { get; init; } = 120;

        // The longest a job may let its compilation run. Unlike zpp's, a job's compilation cannot run without a limit.
        public int MaxCompileTimeoutMilliseconds { get; init; } = 60_000;
    }
}
