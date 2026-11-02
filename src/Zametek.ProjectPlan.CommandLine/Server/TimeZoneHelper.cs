namespace Zametek.ProjectPlan.CommandLine
{
    // The name of a time zone as the IANA database gives it - Europe/London - whatever the platform calls it: Windows says
    // GMT Standard Time, which a program on another system cannot look up.
    internal static class TimeZoneHelper
    {
        public static string GetIanaId(TimeZoneInfo zone)
        {
            ArgumentNullException.ThrowIfNull(zone);

            if (zone.HasIanaId)
            {
                return zone.Id;
            }

            // A Windows name has an IANA name that stands for it, when the system's ICU data can say which. Where it cannot,
            // the platform's own name is better than none.
            return TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out string? ianaId)
                ? ianaId
                : zone.Id;
        }
    }
}
