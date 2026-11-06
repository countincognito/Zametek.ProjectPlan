namespace Zametek.Common.ProjectPlan
{
    /// <summary>
    /// One problem that was found in a file while it was imported, and where it is. A file with one or more of them is not
    /// imported (see <see cref="ScenarioImportException"/>).
    /// </summary>
    /// <remarks>
    /// A sheet, a row and a column are the ones of the workbook: <see cref="Row"/> and <see cref="Column"/> count from 1, as
    /// Excel does, and <see cref="Cell"/> is the address that Excel shows (for example E14). A problem with the file as a
    /// whole has none of them.
    /// </remarks>
    [Serializable]
    public record ImportIssue
    {
        /// <summary>
        /// What kind of problem it is, for programs (see <see cref="ImportIssueCodes"/>). It is never translated.
        /// </summary>
        public string Code { get; init; } = string.Empty;

        /// <summary>
        /// What is wrong, in words, for a person. The words include the place and the value.
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// The name of the sheet, or null when the problem is with the file as a whole.
        /// </summary>
        public string? Sheet { get; init; }

        /// <summary>
        /// The number of the row, counted from 1, or null.
        /// </summary>
        public int? Row { get; init; }

        /// <summary>
        /// The number of the column, counted from 1, or null.
        /// </summary>
        public int? Column { get; init; }

        /// <summary>
        /// The title of the column, or null when it has none.
        /// </summary>
        public string? ColumnTitle { get; init; }

        /// <summary>
        /// The name of the thing that the row describes, for example the name of the activity, or null when the row has none.
        /// </summary>
        public string? RowName { get; init; }

        /// <summary>
        /// The value of the cell as it was read, or null.
        /// </summary>
        public string? Value { get; init; }

        /// <summary>
        /// The address of the cell in the words of Excel (for example E14), or null when the problem is not about one cell.
        /// </summary>
        public string? Cell =>
            Row is int row && Column is int column
                ? $@"{ColumnLetters(column)}{row}"
                : null;

        /// <summary>
        /// The letters that Excel gives to a column: 1 is A, 26 is Z, 27 is AA.
        /// </summary>
        public static string ColumnLetters(int column)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(column, 1);
            var letters = new Stack<char>();

            for (int number = column; number > 0; number = (number - 1) / 26)
            {
                letters.Push((char)('A' + ((number - 1) % 26)));
            }

            return new string([.. letters]);
        }
    }
}
