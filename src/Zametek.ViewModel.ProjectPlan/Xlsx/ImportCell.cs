using System.Globalization;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// The kinds of value that a cell of a workbook holds. A formula holds the kind of its result.
    /// </summary>
    internal enum ImportCellKind
    {
        Blank,
        Text,
        Number,
        Boolean,
        Date,
        Error,
    }

    /// <summary>
    /// A cell of a workbook as the importer sees it: a value of one kind, and nothing of the library that read the file.
    /// A number is a number whatever its format, a date is a cell that is a number with the format of a date, and an error is
    /// an error of Excel (for example #N/A) with its text.
    /// </summary>
    internal readonly record struct ImportCell
    {
        // A value that is longer than this is cut short when it is said in a message.
        private const int c_MaximumDisplayLength = 100;

        private ImportCell(
            ImportCellKind kind,
            string text,
            double number,
            bool boolean,
            DateTime date)
        {
            Kind = kind;
            Text = text;
            Number = number;
            Boolean = boolean;
            Date = date;
        }

        public ImportCellKind Kind { get; }

        /// <summary>The text of a text cell, and the text of an error. Empty for the others.</summary>
        public string Text { get; }

        public double Number { get; }

        public bool Boolean { get; }

        public DateTime Date { get; }

        public static ImportCell Blank => default;

        // A cell that holds text with nothing in it is a cell with no value.
        public static ImportCell OfText(string? text) =>
            string.IsNullOrEmpty(text)
                ? Blank
                : new ImportCell(ImportCellKind.Text, text, 0.0, false, default);

        public static ImportCell OfNumber(double number) =>
            new(ImportCellKind.Number, string.Empty, number, false, default);

        public static ImportCell OfBoolean(bool boolean) =>
            new(ImportCellKind.Boolean, string.Empty, 0.0, boolean, default);

        public static ImportCell OfDate(DateTime date) =>
            new(ImportCellKind.Date, string.Empty, 0.0, false, date);

        public static ImportCell OfError(string text) =>
            new(ImportCellKind.Error, text, 0.0, false, default);

        /// <summary>
        /// Whether the cell has no value: it is blank, or it is text of spaces.
        /// </summary>
        public bool IsEmpty =>
            Kind == ImportCellKind.Blank
            || (Kind == ImportCellKind.Text && string.IsNullOrWhiteSpace(Text));

        /// <summary>
        /// The value as text that does not depend on the culture, as a message says it. A number has no more digits than it
        /// needs, a truth value is TRUE or FALSE as it is in Excel, and a date is the day (and the time, when it has one).
        /// </summary>
        public string Display
        {
            get
            {
                string text = Kind switch
                {
                    ImportCellKind.Text or ImportCellKind.Error => Text,
                    ImportCellKind.Number => Number.ToString(@"R", CultureInfo.InvariantCulture),
                    ImportCellKind.Boolean => Boolean ? @"TRUE" : @"FALSE",
                    ImportCellKind.Date => Date.TimeOfDay == TimeSpan.Zero
                        ? Date.ToString(@"yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : Date.ToString(@"yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    _ => string.Empty,
                };

                return text.Length > c_MaximumDisplayLength
                    ? $@"{text[..c_MaximumDisplayLength]}…"
                    : text;
            }
        }
    }
}
