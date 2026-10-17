using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Reads a workbook into <see cref="ImportTable"/>s. It is the one place of the importer that knows the library that reads
    /// the file: what the importer asks of a workbook is a sheet by its name, and what it gets is a table of cells that each
    /// hold a value of one kind.
    /// </summary>
    internal sealed class XlsxWorkbookReader
    {
        private readonly XSSFWorkbook m_Workbook;
        private readonly ImportIssueCollector m_Issues;

        private XlsxWorkbookReader(
            XSSFWorkbook workbook,
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
        /// Opens the workbook that the stream holds. A file that cannot be read as a workbook is the one problem that the
        /// import reports at once, because there is nothing in it to look for more.
        /// </summary>
        public static XlsxWorkbookReader Open(
            Stream stream,
            ImportIssueCollector issues)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(issues);

            try
            {
                // The library closes the stream that it reads a workbook from, and this one belongs to the caller, so the
                // workbook is read from a copy.
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                copy.Position = 0;

                return new XlsxWorkbookReader(new XSSFWorkbook(copy), issues);
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
            ISheet? sheet = m_Workbook.GetSheet(sheetName);

            if (sheet is null)
            {
                return null;
            }

            HasFoundASheet = true;

            // The titles are the cells of the first row, up to the first one that is blank.
            List<ImportCell> header = [];

            if (sheet.GetRow(0) is IRow titleRow)
            {
                for (int i = 0; i < titleRow.LastCellNum; i++)
                {
                    ImportCell cell = Convert(titleRow.GetCell(i));

                    if (cell.IsEmpty)
                    {
                        break;
                    }

                    header.Add(cell);
                }
            }

            if (header.Count == 0)
            {
                m_Issues.Add(new ImportIssue
                {
                    Code = ImportIssueCodes.MissingHeader,
                    Message = ImportIssueText.MissingHeader(),
                    Sheet = sheet.SheetName,
                });

                return null;
            }

            return ImportTable.Create(sheet.SheetName, header, Rows(sheet, header.Count), knownTitles, m_Issues);
        }

        // The rows under the titles that hold a value in a column that has a title. Each cell is the one in its column, so that
        // a cell that is not there (Excel leaves out the cells that are empty) is a blank cell and no other cell moves.
        private static IEnumerable<(int Number, ImportCell[] Cells)> Rows(
            ISheet sheet,
            int width)
        {
            for (int i = 1; i <= sheet.LastRowNum; i++)
            {
                IRow? row = sheet.GetRow(i);

                if (row is null)
                {
                    continue;
                }

                var cells = new ImportCell[width];
                bool hasValue = false;

                for (int j = 0; j < width; j++)
                {
                    cells[j] = Convert(row.GetCell(j));
                    hasValue |= !cells[j].IsEmpty;
                }

                if (hasValue)
                {
                    yield return (i + 1, cells);
                }
            }
        }

        // A cell that holds a formula holds the result that the file keeps of it.
        private static ImportCell Convert(ICell? cell)
        {
            if (cell is null)
            {
                return ImportCell.Blank;
            }

            CellType type = cell.CellType == CellType.Formula
                ? cell.CachedFormulaResultType
                : cell.CellType;

            switch (type)
            {
                case CellType.String:
                    return ImportCell.OfText(cell.StringCellValue);
                case CellType.Boolean:
                    return ImportCell.OfBoolean(cell.BooleanCellValue);
                case CellType.Numeric:
                    double number = cell.NumericCellValue;

                    // A number with the format of a date is a date, if it is one that a calendar has.
                    return DateUtil.IsCellDateFormatted(cell) && DateUtil.IsValidExcelDate(number)
                        ? ImportCell.OfDate(DateUtil.GetJavaDate(number, cell.Sheet.Workbook is XSSFWorkbook workbook && workbook.IsDate1904()))
                        : ImportCell.OfNumber(number);
                case CellType.Error:
                    return ImportCell.OfError(FormulaError.ForInt(cell.ErrorCellValue).String);
                default:
                    return ImportCell.Blank;
            }
        }
    }
}
