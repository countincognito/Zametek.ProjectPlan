using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// A row of an <see cref="ImportTable"/>, read by the title of the column. Each read gives null when the column is not
    /// there and when the cell has no value, and a value when the cell holds one of the kind that was asked for. A cell that
    /// holds something else is a problem: it is reported, with the place of the cell, and the read gives null.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A cell is read as what it is. A number is a number in every culture, a date is a date, and a truth value is a truth
    /// value. A cell of text can hold a value too, and a person who types a number as text types it as they write it, so
    /// the text is read in the culture of the person who imports. It is read as strictly as it can be, so that it never gives a
    /// value that the person did not write: a number has no thousands separator, and thus "1.5" is 1.5 or it is a problem,
    /// and it is not 15.
    /// </para>
    /// <para>
    /// A cell that holds an error of Excel is a problem whatever the column is.
    /// </para>
    /// </remarks>
    internal sealed class ImportRow
    {
        // The title of the column that names what a row describes. It is said in the messages, for a person to find the row.
        private const string c_NameTitle = @"Name";
        private const int c_MaximumNameLength = 60;

        private readonly ImportTable m_Table;
        private readonly ImportCell[] m_Cells;

        public ImportRow(
            ImportTable table,
            int number,
            ImportCell[] cells)
        {
            m_Table = table;
            Number = number;
            m_Cells = cells;
        }

        /// <summary>
        /// The number of the row in the sheet, counted from 1.
        /// </summary>
        public int Number { get; }

        public ImportTable Table => m_Table;

        #region Cells

        public ImportCell Cell(string title) =>
            m_Table.IndexOf(title) is int index
                ? m_Cells[index]
                : ImportCell.Blank;

        public ImportCell CellAt(int index) =>
            index >= 0 && index < m_Cells.Length
                ? m_Cells[index]
                : ImportCell.Blank;

        #endregion

        #region Reporting

        /// <summary>
        /// Reports a problem with a cell of this row. The value of the problem is what the cell holds, or the part of it that is
        /// the problem when <paramref name="value"/> says so (one place in a list).
        /// </summary>
        public void Report(
            int index,
            string code,
            string message,
            string? value = null)
        {
            ImportCell cell = CellAt(index);

            m_Table.Issues.Add(new ImportIssue
            {
                Code = code,
                Message = message,
                Sheet = m_Table.SheetName,
                Row = Number,
                Column = index + 1,
                ColumnTitle = index < m_Table.Titles.Count ? m_Table.Titles[index] : null,
                RowName = NameOfRow(),
                Value = value ?? (cell.Kind == ImportCellKind.Blank ? null : cell.Display),
            });
        }

        // The name that the row has, if it has a column of names: the activity or the resource is easier to find by it.
        private string? NameOfRow()
        {
            ImportCell cell = Cell(c_NameTitle);

            if (cell.Kind != ImportCellKind.Text)
            {
                return null;
            }

            string name = cell.Text.Trim();

            return name.Length > c_MaximumNameLength
                ? $@"{name[..c_MaximumNameLength]}…"
                : name;
        }

        private void ReportExcelError(int index) =>
            Report(index, ImportIssueCodes.ExcelError, ImportIssueText.ExcelError(CellAt(index).Text));

        #endregion

        #region Text

        public string? Text(string title) =>
            m_Table.IndexOf(title) is int index
                ? TextAt(index)
                : null;

        /// <summary>
        /// The text of a cell. A cell that is not text gives the text that it shows in every culture.
        /// </summary>
        public string? TextAt(int index)
        {
            ImportCell cell = CellAt(index);

            if (cell.Kind == ImportCellKind.Error)
            {
                ReportExcelError(index);
                return null;
            }

            return cell.Kind == ImportCellKind.Text
                ? cell.Text
                : cell.Kind == ImportCellKind.Blank ? string.Empty : DisplayInFull(cell);
        }

        // The text of a number or a date is not cut short, as the text of a message is.
        private static string DisplayInFull(ImportCell cell) =>
            cell.Kind switch
            {
                ImportCellKind.Number => cell.Number.ToString(@"R", CultureInfo.InvariantCulture),
                ImportCellKind.Boolean => cell.Boolean ? @"TRUE" : @"FALSE",
                ImportCellKind.Date => cell.Date.TimeOfDay == TimeSpan.Zero
                    ? cell.Date.ToString(@"yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : cell.Date.ToString(@"yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                _ => cell.Display,
            };

        #endregion

        #region Numbers

        public int? Int(string title) =>
            m_Table.IndexOf(title) is int index
                ? IntAt(index)
                : null;

        public int? IntAt(int index)
        {
            ImportCell cell = CellAt(index);

            switch (cell.Kind)
            {
                case ImportCellKind.Blank:
                    return null;
                case ImportCellKind.Error:
                    ReportExcelError(index);
                    return null;
                case ImportCellKind.Number:
                    if (IsWholeNumber(cell.Number, int.MinValue, int.MaxValue))
                    {
                        return (int)cell.Number;
                    }

                    break;
                case ImportCellKind.Text:
                    if (cell.IsEmpty)
                    {
                        return null;
                    }

                    if (int.TryParse(cell.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out int number))
                    {
                        return number;
                    }

                    break;
            }

            Report(index, ImportIssueCodes.NotAWholeNumber, ImportIssueText.NotAWholeNumber(cell.Display));
            return null;
        }

        public double? Double(string title) =>
            m_Table.IndexOf(title) is int index
                ? DoubleAt(index)
                : null;

        public double? DoubleAt(int index)
        {
            ImportCell cell = CellAt(index);

            switch (cell.Kind)
            {
                case ImportCellKind.Blank:
                    return null;
                case ImportCellKind.Error:
                    ReportExcelError(index);
                    return null;
                case ImportCellKind.Number:
                    if (double.IsFinite(cell.Number))
                    {
                        return cell.Number;
                    }

                    break;
                case ImportCellKind.Text:
                    if (cell.IsEmpty)
                    {
                        return null;
                    }

                    if (double.TryParse(cell.Text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out double number)
                        && double.IsFinite(number))
                    {
                        return number;
                    }

                    break;
            }

            Report(index, ImportIssueCodes.NotANumber, ImportIssueText.NotANumber(cell.Display));
            return null;
        }

        /// <summary>
        /// A whole number of 64 bits that is not negative. A cell holds a whole number exactly only up to 2^53, and thus a
        /// number as large as the seed of a simulation is written as text, in every culture the same.
        /// </summary>
        public ulong? ULong(string title)
        {
            if (m_Table.IndexOf(title) is not int index)
            {
                return null;
            }

            ImportCell cell = CellAt(index);

            switch (cell.Kind)
            {
                case ImportCellKind.Blank:
                    return null;
                case ImportCellKind.Error:
                    ReportExcelError(index);
                    return null;
                case ImportCellKind.Number:
                    if (IsWholeNumber(cell.Number, 0.0, 9007199254740992.0))
                    {
                        return (ulong)cell.Number;
                    }

                    break;
                case ImportCellKind.Text:
                    if (cell.IsEmpty)
                    {
                        return null;
                    }

                    if (ulong.TryParse(cell.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out ulong number))
                    {
                        return number;
                    }

                    break;
            }

            Report(index, ImportIssueCodes.NotAWholeNumber, ImportIssueText.NotAWholeNumber(cell.Display));
            return null;
        }

        private static bool IsWholeNumber(double number, double minimum, double maximum) =>
            double.IsFinite(number)
            && number == Math.Floor(number)
            && number >= minimum
            && number <= maximum;

        #endregion

        #region Truth values

        public bool? Bool(string title) =>
            m_Table.IndexOf(title) is int index
                ? BoolAt(index)
                : null;

        public bool? BoolAt(int index)
        {
            ImportCell cell = CellAt(index);

            switch (cell.Kind)
            {
                case ImportCellKind.Blank:
                    return null;
                case ImportCellKind.Error:
                    ReportExcelError(index);
                    return null;
                case ImportCellKind.Boolean:
                    return cell.Boolean;
                case ImportCellKind.Text:
                    if (cell.IsEmpty)
                    {
                        return null;
                    }

                    if (bool.TryParse(cell.Text.Trim(), out bool value))
                    {
                        return value;
                    }

                    break;
            }

            Report(index, ImportIssueCodes.NotTrueOrFalse, ImportIssueText.NotTrueOrFalse(cell.Display));
            return null;
        }

        #endregion

        #region Dates

        /// <summary>
        /// A date. A cell with the format of a date gives the day that it shows, and a time of day that it holds is not part of
        /// it; a cell of text gives the date that it says, in the culture of the person who imports.
        /// </summary>
        public DateTime? Date(string title) =>
            m_Table.IndexOf(title) is int index
                ? DateAt(index)
                : null;

        public DateTime? DateAt(int index)
        {
            ImportCell cell = CellAt(index);

            switch (cell.Kind)
            {
                case ImportCellKind.Blank:
                    return null;
                case ImportCellKind.Error:
                    ReportExcelError(index);
                    return null;
                case ImportCellKind.Date:
                    return cell.Date.Date;
                case ImportCellKind.Text:
                    if (cell.IsEmpty)
                    {
                        return null;
                    }

                    if (DateTime.TryParse(cell.Text.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime value))
                    {
                        return value;
                    }

                    break;
            }

            Report(index, ImportIssueCodes.NotADate, ImportIssueText.NotADate(cell.Display));
            return null;
        }

        /// <summary>
        /// A date with the offset of the machine, which is what a day without an offset means to the application.
        /// </summary>
        public DateTimeOffset? DateOffset(string title)
        {
            if (m_Table.IndexOf(title) is not int index)
            {
                return null;
            }

            ImportCell cell = CellAt(index);

            if (cell.Kind == ImportCellKind.Text
                && !cell.IsEmpty
                && DateTimeOffset.TryParse(cell.Text.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTimeOffset value))
            {
                return value;
            }

            return DateAt(index) is DateTime date
                ? new DateTimeOffset(date)
                : null;
        }

        #endregion

        #region Choices, colors and lists

        /// <summary>
        /// A choice of an enumeration, written as the name of the value (or its description), in any case.
        /// </summary>
        public T? Option<T>(string title)
            where T : struct, Enum
        {
            if (m_Table.IndexOf(title) is not int index)
            {
                return null;
            }

            ImportCell cell = CellAt(index);

            switch (cell.Kind)
            {
                case ImportCellKind.Blank:
                    return null;
                case ImportCellKind.Error:
                    ReportExcelError(index);
                    return null;
                case ImportCellKind.Text:
                    if (cell.IsEmpty)
                    {
                        return null;
                    }

                    if (Choices<T>.TryParse(cell.Text.Trim(), out T value))
                    {
                        return value;
                    }

                    break;
            }

            Report(index, ImportIssueCodes.NotAChoice, ImportIssueText.NotAChoice(cell.Display, Choices<T>.Names));
            return null;
        }

        /// <summary>
        /// A color written as #RRGGBB or #RRGGBBAA.
        /// </summary>
        public ColorFormatModel? Color(string title)
        {
            if (m_Table.IndexOf(title) is not int index)
            {
                return null;
            }

            ImportCell cell = CellAt(index);

            switch (cell.Kind)
            {
                case ImportCellKind.Blank:
                    return null;
                case ImportCellKind.Error:
                    ReportExcelError(index);
                    return null;
                case ImportCellKind.Text:
                    if (cell.IsEmpty)
                    {
                        return null;
                    }

                    if (ColorHelper.TryHtmlHexCodeToColorFormat(cell.Text.Trim(), out ColorFormatModel value))
                    {
                        return value;
                    }

                    break;
            }

            Report(index, ImportIssueCodes.NotAColor, ImportIssueText.NotAColor(cell.Display));
            return null;
        }

        /// <summary>
        /// A list of whole numbers, written with the separator of the application between them (for example 1,2,3). A single
        /// number is a list of one. A place in the list that is empty is left out, and one that is not a number is a problem.
        /// </summary>
        public List<int> Ids(string title)
        {
            List<int> ids = [];

            if (m_Table.IndexOf(title) is not int index)
            {
                return ids;
            }

            ImportCell cell = CellAt(index);

            switch (cell.Kind)
            {
                case ImportCellKind.Blank:
                    break;
                case ImportCellKind.Error:
                    ReportExcelError(index);
                    break;
                case ImportCellKind.Text:
                    foreach (string part in cell.Text.Split(DependenciesStringValidationRule.Separator))
                    {
                        string trimmed = part.Trim();

                        if (trimmed.Length == 0)
                        {
                            continue;
                        }

                        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.CurrentCulture, out int id))
                        {
                            ids.Add(id);
                        }
                        else
                        {
                            Report(index, ImportIssueCodes.NotAWholeNumber, ImportIssueText.NotAWholeNumber(trimmed), trimmed);
                        }
                    }

                    break;
                default:
                    if (IntAt(index) is int single)
                    {
                        ids.Add(single);
                    }

                    break;
            }

            return ids;
        }

        /// <summary>
        /// The Id of the row: a whole number, and it has to be there.
        /// </summary>
        public int? RequiredId(string title)
        {
            if (m_Table.IndexOf(title) is not int index)
            {
                return null;
            }

            if (CellAt(index).IsEmpty)
            {
                Report(index, ImportIssueCodes.MissingValue, ImportIssueText.MissingValue());
                return null;
            }

            return IntAt(index);
        }

        /// <summary>
        /// The words that an enumeration can be written as, found once.
        /// </summary>
        private static class Choices<T>
            where T : struct, Enum
        {
            private static readonly (T Value, string Name, string? Description)[] s_Choices =
                [.. Enum.GetValues<T>().Select(x => (x, x.ToString(), typeof(T).GetField(x.ToString())?.GetCustomAttribute<DescriptionAttribute>()?.Description))];

            public static string Names { get; } = string.Join(@", ", s_Choices.Select(x => x.Name));

            public static bool TryParse(string text, out T value)
            {
                foreach ((T choice, string name, string? description) in s_Choices)
                {
                    if (string.Equals(text, name, StringComparison.OrdinalIgnoreCase)
                        || (description is not null && string.Equals(text, description, StringComparison.OrdinalIgnoreCase)))
                    {
                        value = choice;
                        return true;
                    }
                }

                value = default;
                return false;
            }
        }

        #endregion
    }
}
