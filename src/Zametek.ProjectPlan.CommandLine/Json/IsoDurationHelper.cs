using System.Text.RegularExpressions;
using System.Xml;

namespace Zametek.ProjectPlan.CommandLine
{
    // Durations as zpp serve's API writes them: ISO 8601, in days, hours, minutes and seconds - PT5S, PT1M, P1DT2H - and in
    // nothing longer, because a month or a year has no length of its own.
    internal static class IsoDurationHelper
    {
        private static readonly Regex s_Pattern = new(@"^P(?:\d+D)?(?:T(?:\d+H)?(?:\d+M)?(?:\d+(?:\.\d+)?S)?)?$", RegexOptions.CultureInvariant);

        // The duration written as ISO 8601 writes it: two minutes as PT2M, a thousandth of a second as PT0.001S.
        public static string ToString(TimeSpan duration)
        {
            return XmlConvert.ToString(duration);
        }

        // Whether the text is a duration of days, hours, minutes and seconds, and which.
        public static bool TryParse(
            string? text,
            out TimeSpan duration)
        {
            duration = default;

            // Something after the P, and something after a T.
            if (text is null
                || text == @"P"
                || text.EndsWith('T')
                || !s_Pattern.IsMatch(text))
            {
                return false;
            }

            try
            {
                duration = XmlConvert.ToTimeSpan(text);
                return true;
            }
            catch (Exception ex) when (ex is FormatException or OverflowException)
            {
                return false;
            }
        }
    }
}
