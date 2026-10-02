namespace Zametek.ProjectPlan.CommandLine
{
    // What zpp serve says about itself: its version, the culture its jobs write numbers and dates in, the time zone
    // their times are in, and the limits it works within.
    public record InfoResponse(
        string Version,
        string Culture,
        string TimeZone,
        ServeLimits Limits);
}
