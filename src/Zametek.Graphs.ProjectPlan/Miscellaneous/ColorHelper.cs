using Avalonia.Media;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Zametek.Graphs.ProjectPlan
{
    // A focused copy of the colour helpers the graph rendering needs, kept local to this library
    // so it does not depend on the application's ViewModel project. (The application keeps its own
    // fuller ColorHelper; a little redundancy here is deliberate to make the library standalone.)
    // Works purely in HTML hex strings and Avalonia colours so the library carries no dependency on
    // the application's ColorFormatModel. Internal so it never clashes with the application's public
    // ColorHelper in consumers that import both namespaces.
    internal static class ColorHelper
    {
        public const byte AnnotationAFull = 255;

        // These match the SVG theme backgrounds used by the MSAGL renderer.
        public static readonly string SvgLightThemeBackground = "white";
        public static readonly Color LightThemeBackground = Colors.White;

        public static readonly string SvgDarkThemeBackground = "#373737";
        public static readonly Color DarkThemeBackground = Color.FromArgb(AnnotationAFull, 55, 55, 55);

        private static readonly Regex s_HtmlHexMatch = new(@"^#(([A-Fa-f0-9]{2}){3,4})$", RegexOptions.Compiled);

        public static string ColorToHtmlHexCode(Color color)
        {
            return BytesToHtmlHexCode(color.R, color.G, color.B, color.A);
        }

        public static Color HtmlHexCodeToColor(string input)
        {
            MatchCollection matches = s_HtmlHexMatch.Matches(input);

            if (matches.Count > 0
                && matches[0].Groups.Count > 1)
            {
                string value = matches[0].Groups[1].Value;
                byte[] bytes = Convert.FromHexString(value);

                if (bytes.Length == 3)
                {
                    return Color.FromArgb(byte.MaxValue, bytes[0], bytes[1], bytes[2]);
                }
                else if (bytes.Length == 4)
                {
                    return Color.FromArgb(bytes[3], bytes[0], bytes[1], bytes[2]);
                }
            }

            return Color.FromArgb(0, 0, 0, 0);
        }

        public static string BytesToHtmlHexCode(byte r, byte g, byte b)
        {
            return $@"#{r.ByteToHexString()}{g.ByteToHexString()}{b.ByteToHexString()}";
        }

        public static string BytesToHtmlHexCode(byte a, byte r, byte g, byte b)
        {
            return $@"#{a.ByteToHexString()}{r.ByteToHexString()}{g.ByteToHexString()}{b.ByteToHexString()}";
        }

        private static string ByteToHexString(this byte input)
        {
            return input.ToString(@"X2", CultureInfo.InvariantCulture);
        }
    }
}
