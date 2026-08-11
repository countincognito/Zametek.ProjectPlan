using System;
using System.Globalization;

namespace Zametek.View.ProjectPlan
{
    // Shared pieces for building clipboard/text output from DataGrid content.
    public static class DataGridHelper
    {
        // The BCL offers no named constants for these characters (Environment.NewLine
        // is the platform newline *string*), so name them here.
        public const char Tab = '\t';
        public const char CarriageReturn = '\r';
        public const char LineFeed = '\n';
        public const char Space = ' ';

        // Raw, invariant-culture cell values (not display-formatted), so copied
        // tables are calculation-friendly when pasted into a spreadsheet.
        public static string FormatCellValue(object? value) =>
            value switch
            {
                null => string.Empty,
                double doubleValue => doubleValue.ToString(CultureInfo.InvariantCulture),
                int intValue => intValue.ToString(CultureInfo.InvariantCulture),
                string stringValue => EscapeCellText(stringValue),
                _ => EscapeCellText(value.ToString() ?? string.Empty),
            };

        // Cell content can be free text: strip the characters that would corrupt
        // a tab-separated layout.
        public static string EscapeCellText(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return value.Replace(Tab, Space).Replace(CarriageReturn, Space).Replace(LineFeed, Space);
        }
    }
}
