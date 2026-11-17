using ClosedXML.Excel;
using System.IO.Compression;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Reads a workbook into <see cref="ImportTable"/>s. It is the one place of the importer that knows the library that reads
    /// the file: what the importer asks of a workbook is a sheet by its name, and what it gets is a table of cells that each
    /// hold a value of one kind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A workbook is a file that anyone can make. A file of some kilobytes can say that a sheet has a million rows and
    /// thousands of titles, and its parts can unpack to many times the size of the file. So the reader goes only to the rows
    /// and to the cells that the file holds, and it has three bounds: the size of the parts when they are unpacked, the rows
    /// of a sheet, and the cells of the workbook. A workbook above a bound is refused at once, with one problem.
    /// </para>
    /// <para>
    /// The library loads the whole workbook, and that load cannot be stopped. The bound on the size of the parts is thus
    /// the bound on the time and on the memory of the load. After the load, a cancellation stops the reader between two
    /// rows.
    /// </para>
    /// </remarks>
    internal sealed class XlsxWorkbookReader : IDisposable
    {
        // The error that Excel shows for a formula it cannot work out.
        private const string c_UnworkableFormula = @"#VALUE!";

        private const long c_Megabyte = 1024 * 1024;

        /// <summary>
        /// The largest size of the parts of a workbook, when they are unpacked. The parts of a workbook are packed, and a
        /// part that holds the same thing many times packs to almost nothing.
        /// </summary>
        public const long MaximumUnpackedBytes = 100 * c_Megabyte;

        /// <summary>
        /// The largest number of rows of a sheet that hold a value under a title. The import makes an item of the plan for
        /// each row. It is five times the largest number of activities that a plan can have.
        /// </summary>
        public const int MaximumRows = 10_000;

        /// <summary>
        /// The largest number of cells that the reader reads in one workbook: the titles, and each row from its first cell
        /// to its last cell that holds something.
        /// </summary>
        public const long MaximumCells = 10_000_000;

        private readonly XLWorkbook m_Workbook;
        private readonly ImportIssueCollector m_Issues;
        private readonly CancellationToken m_CancellationToken;

        private XlsxWorkbookReader(
            XLWorkbook workbook,
            ImportIssueCollector issues,
            CancellationToken cancellationToken)
        {
            m_Workbook = workbook;
            m_Issues = issues;
            m_CancellationToken = cancellationToken;
        }

        /// <summary>
        /// The number of cells that the reader has read.
        /// </summary>
        public long CellsRead { get; private set; }

        /// <summary>
        /// Whether the workbook had a sheet with a name that was asked for. A workbook that has none of the sheets that the
        /// importer reads is not a workbook of the application.
        /// </summary>
        public bool HasFoundASheet { get; private set; }

        /// <summary>
        /// Opens the workbook that the stream holds, and leaves the stream open. A file that cannot be read as a workbook is
        /// a problem that the import reports at once, because there is nothing in it to look for more. A workbook with parts
        /// that are too large is refused before the library unpacks them.
        /// </summary>
        public static XlsxWorkbookReader Open(
            Stream stream,
            ImportIssueCollector issues,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(issues);
            cancellationToken.ThrowIfCancellationRequested();

            if (UnpackedLength(stream) is long unpacked
                && unpacked > MaximumUnpackedBytes)
            {
                issues.Add(new ImportIssue
                {
                    Code = ImportIssueCodes.WorkbookTooLarge,
                    Message = ImportIssueText.WorkbookTooLarge((unpacked + c_Megabyte - 1) / c_Megabyte, MaximumUnpackedBytes / c_Megabyte),
                });

                throw issues.ToException();
            }

            try
            {
                return new XlsxWorkbookReader(new XLWorkbook(stream, XlsxGraphicEngine.CreateLoadOptions()), issues, cancellationToken);
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

        // The size that the parts of the file say that they have when they are unpacked, or null for a file that is not a
        // package (the library then says why it cannot read the file) and for a stream that cannot go back to its place.
        // A part cannot unpack to more than it says: the reader of the package stops at that size.
        private static long? UnpackedLength(Stream stream)
        {
            if (!stream.CanSeek)
            {
                return null;
            }

            long start = stream.Position;

            try
            {
                using var package = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
                long total = 0;

                foreach (ZipArchiveEntry entry in package.Entries)
                {
                    // A size above the bound is all that the caller needs to know, and a sum that stops there cannot overflow.
                    total += Math.Min(Math.Max(entry.Length, 0), MaximumUnpackedBytes + 1);

                    if (total > MaximumUnpackedBytes)
                    {
                        break;
                    }
                }

                return total;
            }
            catch (InvalidDataException)
            {
                return null;
            }
            finally
            {
                stream.Position = start;
            }
        }

        // Counts the cells that the reader is about to read, and stops the import when the workbook has too many.
        private void CountCells(int count)
        {
            CellsRead += count;

            if (CellsRead > MaximumCells)
            {
                m_Issues.Add(new ImportIssue
                {
                    Code = ImportIssueCodes.TooManyCells,
                    Message = ImportIssueText.TooManyCells(MaximumCells),
                });

                throw m_Issues.ToException();
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

            if (!m_Workbook.TryGetWorksheet(sheetName, out IXLWorksheet? sheet))
            {
                return null;
            }

            HasFoundASheet = true;

            // The titles are the cells of the first row, up to the first one that is blank.
            List<ImportCell> header = [];
            int lastTitle = sheet.Row(1).LastCellUsed(XLCellsUsedOptions.Contents)?.Address.ColumnNumber ?? 0;
            m_CancellationToken.ThrowIfCancellationRequested();
            CountCells(lastTitle);

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
        //
        // The reader goes to the rows that the file holds, and not to each number up to the last row. It reads a row up to
        // its last cell that holds something, and not up to the last title: the cells after that one are blank (ImportRow
        // gives a blank cell for a place after the end of its cells).
        private IEnumerable<(int Number, ImportCell[] Cells)> Rows(
            IXLWorksheet sheet,
            int width)
        {
            int count = 0;

            foreach (IXLRow used in sheet.RowsUsed(XLCellsUsedOptions.Contents))
            {
                m_CancellationToken.ThrowIfCancellationRequested();
                int row = used.RowNumber();

                if (row < 2)
                {
                    continue;
                }

                int length = Math.Min(width, used.LastCellUsed(XLCellsUsedOptions.Contents)?.Address.ColumnNumber ?? 0);
                CountCells(length);
                var cells = new ImportCell[length];
                bool hasValue = false;

                for (int column = 0; column < length; column++)
                {
                    cells[column] = Convert(sheet.Cell(row, column + 1));
                    hasValue |= !cells[column].IsEmpty;
                }

                if (!hasValue)
                {
                    continue;
                }

                count++;

                if (count > MaximumRows)
                {
                    m_Issues.Add(new ImportIssue
                    {
                        Code = ImportIssueCodes.TooManyRows,
                        Message = ImportIssueText.TooManyRows(MaximumRows),
                        Sheet = sheet.Name,
                    });

                    throw m_Issues.ToException();
                }

                yield return (row, cells);
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
