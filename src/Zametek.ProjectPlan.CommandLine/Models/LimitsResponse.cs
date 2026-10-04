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
        TimeSpan JobTimeout,
        TimeSpan MaxCompileTimeout)
    {
        public static LimitsResponse From(ServeLimits limits)
        {
            ArgumentNullException.ThrowIfNull(limits);

            return new LimitsResponse(
                limits.MaxJobs,
                limits.MaxQueue,
                limits.MaxUploadMegabytes,
                limits.MaxChartWidth,
                limits.MaxChartHeight,
                TimeSpan.FromSeconds(limits.JobTimeoutSeconds),
                TimeSpan.FromMilliseconds(limits.MaxCompileTimeoutMilliseconds));
        }
    }
}
