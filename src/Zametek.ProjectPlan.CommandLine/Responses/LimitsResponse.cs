namespace Zametek.ProjectPlan.CommandLine
{
    // The limits zpp serve works within, as /v1/info gives them. They are ServeLimits, but for what a program that is not
    // zpp serve reads of them: a duration is a duration - ISO 8601, as the options of a request give one - and its unit is
    // not in its name.
    public record LimitsResponse(
        int MaxJobs,
        int MaxQueue,
        int MaxUploadMegabytes,
        int MaxChartWidth,
        int MaxChartHeight,
        int MaxSimulationIterations,
        TimeSpan JobTimeout,
        TimeSpan MaxCompileTimeout)
    {
        // The connections that the server keeps open at once. It is not in the list above, so that the answer of a server
        // before 0.11.0-beta.3, which does not have it, is still read: it is then 0.
        public int MaxConnections { get; init; }

        public static LimitsResponse From(ServeLimits limits)
        {
            ArgumentNullException.ThrowIfNull(limits);

            return new LimitsResponse(
                limits.MaxJobs,
                limits.MaxQueue,
                limits.MaxUploadMegabytes,
                limits.MaxChartWidth,
                limits.MaxChartHeight,
                limits.MaxSimulationIterations,
                TimeSpan.FromSeconds(limits.JobTimeoutSeconds),
                TimeSpan.FromMilliseconds(limits.MaxCompileTimeoutMilliseconds))
            {
                MaxConnections = limits.MaxConnections,
            };
        }
    }
}
