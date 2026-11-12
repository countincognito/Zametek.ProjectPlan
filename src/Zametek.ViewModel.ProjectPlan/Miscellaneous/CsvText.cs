using System.Buffers;
using System.Globalization;

namespace Zametek.ViewModel.ProjectPlan
{
    // The CSV of the files that the application writes: the numbers in the invariant culture, a line feed at the end of each
    // line, and a field with a comma, a quote or a line end in quotes, as RFC 4180 says.
    internal static class CsvText
    {
        private static readonly SearchValues<char> s_QuotedCharacters = SearchValues.Create(",\"\r\n");

        public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        public static string Number(int? value) => value is int number ? Number(number) : string.Empty;

        public static string Number(double value) => value.ToString(@"R", CultureInfo.InvariantCulture);

        public static string Boolean(bool value) => value ? @"true" : @"false";

        public static void WriteRow(TextWriter writer, params string[] fields)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0)
                {
                    writer.Write(',');
                }
                writer.Write(Quote(fields[i]));
            }
            writer.Write(NewLineHelper.NewLine);
        }

        private static string Quote(string field)
        {
            if (field.AsSpan().IndexOfAny(s_QuotedCharacters) < 0)
            {
                return field;
            }
            return $@"""{field.Replace(@"""", @"""""", StringComparison.Ordinal)}""";
        }
    }
}
