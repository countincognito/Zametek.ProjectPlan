using ClosedXML.Excel;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Reads a workbook into <see cref="ImportTable"/>s. It is the one place of the importer that knows the library that reads
    /// the file: what the importer asks of a workbook is a sheet by its name, and what it gets is a table of cells that each
    /// hold a value of one kind.
    /// </summary>
    internal sealed class XlsxWorkbookReader : IDisposable
    {
        // The error that Excel shows for a formula it cannot work out.
        private const string c_UnworkableFormula = @"#VALUE!";

        private readonly XLWorkbook m_Workbook;
        private readonly ImportIssueCollector m_Issues;

        private XlsxWorkbookReader(
            XLWorkbook workbook,
            ImportIssueCollector issues)
        {
            m_Workbook = workbook;
            m_Issues = issues;
        }

        /// <summary>
        /// Whether the workbook had a sheet with a name that was asked for. A workbook that has none of the sheets that the
        /// importer reads is not a workbook of the application.
        /// </summary>
        public bool HasFoundASheet { get; private set; }

        /// <summary>
        /// Opens the workbook that the stream holds, and leaves the stream open. A file that cannot be read as a workbook is
        /// the one problem that the import reports at once, because there is nothing in it to look for more.
        /// </summary>
        public static XlsxWorkbookReader Open(
            Stream stream,
            ImportIssueCollector issues)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(issues);

            try
            {
                return new XlsxWorkbookReader(new XLWorkbook(stream, XlsxGraphicEngine.CreateLoadOptions()), issues);
            }
            catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
            {
                issues.Add(new ImportIssue
                {
                    Code = ImportIssueCodes.UnreadableWorkbook,
                    Message = ImportIssueText.UnreadableWorkbook(ex.Message),
                });

                throw issues.ToException();
            }
        }

        public void Dispose() => m_Workbook.Dispose();

        /// <summary>
        /// The table of the sheet with this name (the case of the letters does not matter), or null when there is no such sheet,
        /// or when it has no row of titles, which is reported.
        /// </summary>
        /// <param name="sheetName">The name of the sheet.</param>
        /// <param name="knownTitles">The titles that the importer reads in the sheet.</param>
        public ImportTable? Table(
            string sheetName,
            IReadOnlyList<string> knownTitles)
        {
            ArgumentNullException.ThrowIfNull(sheetName);
            ArgumentNullException.ThrowIfNull(knownTitles);

            if (!m_Workbook.TryGetWorksheet(sheetName, out IXLWorksheet? sheet))
            {
                return null;
            }

            HasFoundASheet = true;

            // The titles are the cells of the first row, up to the first one that is blank.
            List<ImportCell> header = [];
            int lastTitle = sheet.Row(1).LastCellUsed(XLCellsUsedOptions.Contents)?.Address.ColumnNumber ?? 0;

            for (int column = 1; column <= lastTitle; column++)
            {
                ImportCell cell = Convert(sheet.Cell(1, column));

                if (cell.IsEmpty)
                {
                    break;
                }

                header.Add(cell);
            }

            if (header.Count == 0)
            {
                m_Issues.Add(new ImportIssue
                {
                    Code = ImportIssueCodes.MissingHeader,
                    Message = ImportIssueText.MissingHeader(),
                    Sheet = sheet.Name,
                });

                return null;
            }

            return ImportTable.Create(sheet.Name, header, Rows(sheet, header.Count), knownTitles, m_Issues);
        }

        // The rows under the titles that hold a value in a column that has a title. Each cell is the one in its column, so that
        // a cell that is not there (Excel leaves out the cells that are empty) is a blank cell and no other cell moves. The
        // rows and the columns are counted from 1 in the library, as they are in the sheet.
        private static IEnumerable<(int Number, ImportCell[] Cells)> Rows(
            IXLWorksheet sheet,
            int width)
        {
            int lastRow = sheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 0;

            for (int row = 2; row <= lastRow; row++)
            {
                var cells = new ImportCell[width];
                bool hasValue = false;

                for (int column = 0; column < width; column++)
                {
                    cells[column] = Convert(sheet.Cell(row, column + 1));
                    hasValue |= !cells[column].IsEmpty;
                }

                if (hasValue)
                {
                    yield return (row, cells);
                }
            }
        }

        // A cell that holds a formula holds the result that the file keeps of it (or, when the file keeps none, the result
        // that the library works out).
        private static ImportCell Convert(IXLCell cell)
        {
            XLCellValue value;

            try
            {
                value = cell.Value;
            }
            catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
            {
                // A formula that the library cannot work out (it has a function that the library does not know, or it
                // refers to itself) has no result to read.
                return ImportCell.OfError(c_UnworkableFormula);
            }

            switch (value.Type)
            {
                case XLDataType.Text:
                    return ImportCell.OfText(value.GetText());
                case XLDataType.Boolean:
                    return ImportCell.OfBoolean(value.GetBoolean());
                case XLDataType.Number:
                    return ImportCell.OfNumber(value.GetNumber());
                case XLDataType.DateTime:
                    return ToDate(value);
                case XLDataType.TimeSpan:
                    // A time of the day (or an amount of time) is not a date.
                    return ImportCell.OfNumber(value.GetUnifiedNumber());
                case XLDataType.Error:
                    return ImportCell.OfError(value.ToString());
                default:
                    return ImportCell.Blank;
            }
        }

        // A number with the format of a date is a date, if it is one that a calendar has: that is not so for a number before
        // the first day (a negative one) or for one past the last day of the year 9999.
        private static ImportCell ToDate(XLCellValue value)
        {
            double number = value.GetUnifiedNumber();

            if (number < 0.0)
            {
                return ImportCell.OfNumber(number);
            }

            try
            {
                return ImportCell.OfDate(value.GetDateTime());
            }
            catch (ArgumentException)
            {
                return ImportCell.OfNumber(number);
            }
        }
    }
}
