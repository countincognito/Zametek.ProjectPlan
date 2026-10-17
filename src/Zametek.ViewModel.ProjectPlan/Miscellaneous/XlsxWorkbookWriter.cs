using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.IO.Compression;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// How a cell looks. A look is bold 12 point text for a title, a date format for a date, or both.
    /// </summary>
    internal enum XlsxCellStyle
    {
        Plain,
        Title,
        Date,
        TitleDate,
    }

    /// <summary>
    /// The looks of dates that the sheets of one workbook share. The sheets of a writer that make dates need one; a sheet that
    /// holds none does not.
    /// </summary>
    internal sealed class XlsxDateStyles
    {
        internal XlsxDateStyles(
            ICellStyle date,
            ICellStyle? titleDate)
        {
            Date = date;
            TitleDate = titleDate;
        }

        internal ICellStyle Date { get; }

        internal ICellStyle? TitleDate { get; }
    }

    /// <summary>
    /// Writes a workbook. It is the one place of the exporter that knows the library that writes the file: what the exporter says
    /// to it is a sheet by its name, and in it a cell, by its row and its column (both counted from 0), that holds a text, a
    /// number, a truth value or a date, in one of the four looks of <see cref="XlsxCellStyle"/>.
    /// </summary>
    /// <remarks>
    /// The calls make the file in the order in which they come: the library numbers its strings and its looks as it meets them,
    /// and thus the same calls in the same order give the same file.
    /// </remarks>
    internal sealed class XlsxWorkbookWriter
    {
        private readonly XSSFWorkbook m_Workbook;
        private readonly ICellStyle m_TitleStyle;

        public XlsxWorkbookWriter()
        {
            m_Workbook = new XSSFWorkbook();

            IFont titleFont = m_Workbook.CreateFont();
            titleFont.IsBold = true;
            titleFont.FontHeightInPoints = 12;

            m_TitleStyle = m_Workbook.CreateCellStyle();
            m_TitleStyle.SetFont(titleFont);
        }

        /// <summary>
        /// Makes the looks of dates for the sheets that are added with them. A title with a date has the look of a title and the
        /// format of a date, and so it is made only when it is asked for.
        /// </summary>
        public XlsxDateStyles CreateDateStyles(
            string dateFormat,
            bool includeTitleDate = false)
        {
            ArgumentNullException.ThrowIfNull(dateFormat);

            ICellStyle date = m_Workbook.CreateCellStyle();
            date.DataFormat = m_Workbook.GetCreationHelper().CreateDataFormat().GetFormat(dateFormat);
            ICellStyle? titleDate = null;

            if (includeTitleDate)
            {
                titleDate = m_Workbook.CreateCellStyle();
                titleDate.CloneStyleFrom(m_TitleStyle);
                titleDate.DataFormat = m_Workbook.GetCreationHelper().CreateDataFormat().GetFormat(dateFormat);
            }

            return new XlsxDateStyles(date, titleDate);
        }

        /// <summary>
        /// Adds a sheet at the end of the workbook.
        /// </summary>
        /// <param name="name">The name of the sheet.</param>
        /// <param name="dateStyles">The looks of the dates that the sheet holds, or null when it holds none.</param>
        public XlsxSheetWriter AddSheet(
            string name,
            XlsxDateStyles? dateStyles = null)
        {
            ArgumentNullException.ThrowIfNull(name);

            return new XlsxSheetWriter(m_Workbook.CreateSheet(name), m_TitleStyle, dateStyles);
        }

        /// <summary>
        /// Writes the workbook to the stream, and leaves the stream open.
        /// </summary>
        /// <remarks>
        /// The library stamps the workbook with the wall clock twice over: as the time it was created, and as the time each part
        /// of the package was last written, as it zips them. Both are stamped here with the time that the caller gives instead, so
        /// that a workbook written at a given time is the same workbook wherever and however it is written. That means that the
        /// workbook is zipped to memory first, to restamp the parts.
        /// </remarks>
        public void Save(
            Stream stream,
            DateTimeOffset localNow)
        {
            ArgumentNullException.ThrowIfNull(stream);

            m_Workbook.GetProperties().CoreProperties.Created = localNow.UtcDateTime;

            using var package = new MemoryStream();
            m_Workbook.Write(package, leaveOpen: true);
            StampEntries(package, localNow);

            // The caller owns the stream, so it is left open.
            package.Position = 0;
            package.CopyTo(stream);
        }

        // Restamps every part of a zipped package as last written at the given time. A zip holds the local clock time
        // it was given, to the nearest two seconds and only between 1980 and 2107, so a time outside that is held at
        // the nearer end. Updating an entry's time rewrites its headers but not its compressed data.
        private static void StampEntries(MemoryStream package, DateTimeOffset localNow)
        {
            DateTimeOffset earliest = new(1980, 1, 1, 0, 0, 0, localNow.Offset);
            DateTimeOffset latest = new(2107, 12, 31, 23, 59, 58, localNow.Offset);
            DateTimeOffset lastWriteTime = localNow < earliest ? earliest : localNow > latest ? latest : localNow;

            package.Position = 0;
            using var archive = new ZipArchive(package, ZipArchiveMode.Update, leaveOpen: true);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                entry.LastWriteTime = lastWriteTime;
            }
        }
    }

    /// <summary>
    /// A sheet of an <see cref="XlsxWorkbookWriter"/>. Each call makes the cell that it names (and the row, if the sheet has not
    /// got it): a cell is written once.
    /// </summary>
    internal sealed class XlsxSheetWriter
    {
        private readonly ISheet m_Sheet;
        private readonly ICellStyle m_TitleStyle;
        private readonly XlsxDateStyles? m_DateStyles;

        internal XlsxSheetWriter(
            ISheet sheet,
            ICellStyle titleStyle,
            XlsxDateStyles? dateStyles)
        {
            m_Sheet = sheet;
            m_TitleStyle = titleStyle;
            m_DateStyles = dateStyles;
        }

        /// <summary>
        /// A cell with no value.
        /// </summary>
        public void Blank(int row, int column, XlsxCellStyle style = XlsxCellStyle.Plain) =>
            Create(row, column, style);

        /// <summary>
        /// A cell of text. No text is a cell with no value.
        /// </summary>
        public void Text(int row, int column, string? text, XlsxCellStyle style = XlsxCellStyle.Plain) =>
            Create(row, column, style).SetCellValue(text);

        public void Number(int row, int column, double number, XlsxCellStyle style = XlsxCellStyle.Plain) =>
            Create(row, column, style).SetCellValue(number);

        public void Boolean(int row, int column, bool value, XlsxCellStyle style = XlsxCellStyle.Plain) =>
            Create(row, column, style).SetCellValue(value);

        /// <summary>
        /// A date. The cell has the format of a date, and so the style is <see cref="XlsxCellStyle.Date"/> or
        /// <see cref="XlsxCellStyle.TitleDate"/>.
        /// </summary>
        public void Date(int row, int column, DateTime date, XlsxCellStyle style = XlsxCellStyle.Date) =>
            Create(row, column, style).SetCellValue(date);

        private ICell Create(int row, int column, XlsxCellStyle style)
        {
            IRow sheetRow = m_Sheet.GetRow(row) ?? m_Sheet.CreateRow(row);
            ICell cell = sheetRow.CreateCell(column);

            switch (style)
            {
                case XlsxCellStyle.Plain:
                    break;
                case XlsxCellStyle.Title:
                    cell.CellStyle = m_TitleStyle;
                    break;
                case XlsxCellStyle.Date:
                    cell.CellStyle = (m_DateStyles ?? throw new InvalidOperationException()).Date;
                    break;
                case XlsxCellStyle.TitleDate:
                    cell.CellStyle = (m_DateStyles?.TitleDate) ?? throw new InvalidOperationException();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(style), style, null);
            }

            return cell;
        }
    }
}
