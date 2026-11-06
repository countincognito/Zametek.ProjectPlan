using ClosedXML.Excel;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

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
            string dateFormat,
            bool hasTitleDate)
        {
            DateFormat = dateFormat;
            HasTitleDate = hasTitleDate;
        }

        internal string DateFormat { get; }

        internal bool HasTitleDate { get; }
    }

    /// <summary>
    /// Writes a workbook. It is the one place of the exporter that knows the library that writes the file: what the exporter says
    /// to it is a sheet by its name, and in it a cell, by its row and its column (both counted from 0), that holds a text, a
    /// number, a truth value or a date, in one of the four looks of <see cref="XlsxCellStyle"/>.
    /// </summary>
    /// <remarks>
    /// A number is kept to 15 significant digits, which is what Excel keeps. A number that is not a number (NaN) or that no
    /// number can hold (infinity) is an error of Excel in its cell, as it is when Excel makes a sheet: #NUM! for NaN and
    /// #DIV/0! for infinity, and #NUM! for a number too large for the 15 digits.
    /// </remarks>
    internal sealed class XlsxWorkbookWriter : IDisposable
    {
        private readonly XLWorkbook m_Workbook;

        public XlsxWorkbookWriter()
        {
            m_Workbook = new XLWorkbook(XlsxGraphicEngine.CreateLoadOptions());
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

            return new XlsxDateStyles(dateFormat, includeTitleDate);
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

            return new XlsxSheetWriter(m_Workbook.AddWorksheet(name), dateStyles);
        }

        /// <summary>
        /// Writes the workbook to the stream, and leaves the stream open.
        /// </summary>
        /// <remarks>
        /// The library stamps the workbook with the wall clock as the time it was created and modified, and it gives a part of
        /// the package a name made of a random number, and two of the relationships of the package a random identifier. The
        /// times are the one that the caller gives instead, the part and the relationships get names that are the same each
        /// time, and each part of the package is stamped with the time of the caller as the time it was last written (see
        /// <see cref="Package"/>). Thus a workbook written at a given time is the same workbook wherever and however it is
        /// written, as far as the parts of the package go (the runtime that compresses them may do it in a different way, and
        /// so the bytes of the file may differ from one platform to another).
        /// </remarks>
        public void Save(
            Stream stream,
            DateTimeOffset localNow)
        {
            ArgumentNullException.ThrowIfNull(stream);

            m_Workbook.Properties.Created = localNow.UtcDateTime;
            m_Workbook.Properties.Modified = localNow.UtcDateTime;

            // The library can write only to a stream that it can seek in.
            using var saved = new MemoryStream();
            m_Workbook.SaveAs(saved);
            saved.Position = 0;

            Package.Normalise(saved, stream, localNow);
        }

        public void Dispose() => m_Workbook.Dispose();

        /// <summary>
        /// Makes the zip of a workbook the same wherever it was written.
        /// </summary>
        private static class Package
        {
            private const string c_PropertiesFolder = @"package/services/metadata/core-properties/";
            private const string c_PropertiesExtension = @".psmdcp";
            private const string c_PropertiesName = c_PropertiesFolder + @"core" + c_PropertiesExtension;
            private const string c_Relationships = @"_rels/.rels";

            // The identifiers of relationships that the library makes: an R and 16 hexadecimal digits.
            private static readonly Regex s_RandomIdentifier = new(@"(?<=\sId="")R[0-9a-fA-F]{16}(?="")", RegexOptions.CultureInvariant);

            /// <summary>
            /// Copies the zip to the stream with the part of the properties named <c>core</c>, the relationships that had a
            /// random identifier numbered in the order in which they come, and every part last written at the given time. The
            /// parts keep their order.
            /// </summary>
            /// <remarks>
            /// A zip holds the local clock time it was given, to the nearest two seconds and only between 1980 and 2107, so a
            /// time outside that is held at the nearer end.
            /// </remarks>
            public static void Normalise(
                Stream saved,
                Stream output,
                DateTimeOffset localNow)
            {
                DateTimeOffset earliest = new(1980, 1, 1, 0, 0, 0, localNow.Offset);
                DateTimeOffset latest = new(2107, 12, 31, 23, 59, 58, localNow.Offset);
                DateTimeOffset lastWriteTime = localNow < earliest ? earliest : localNow > latest ? latest : localNow;

                List<(string Name, byte[] Content)> parts = [];
                using (var archive = new ZipArchive(saved, ZipArchiveMode.Read, leaveOpen: true))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        using Stream stream = entry.Open();
                        using var content = new MemoryStream();
                        stream.CopyTo(content);
                        parts.Add((entry.FullName, content.ToArray()));
                    }
                }

                Rename(parts);

                // The zip is made in memory, because an archive makes a different zip for a stream that cannot be seeked
                // in (it adds to each part a record of its size after it), and the stream of the caller may be one.
                using var zipped = new MemoryStream();
                using (var archive = new ZipArchive(zipped, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach ((string name, byte[] content) in parts)
                    {
                        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                        entry.LastWriteTime = lastWriteTime;
                        using Stream stream = entry.Open();
                        stream.Write(content);
                    }
                }

                zipped.Position = 0;
                zipped.CopyTo(output);
            }

            // The part of the properties has a name that is a new random number each time, and the relationships name it. (The
            // name is not made of the content of the part, because that holds the time, and the parts of two workbooks that
            // were made at two times would have two names.)
            private static void Rename(List<(string Name, byte[] Content)> parts)
            {
                int properties = parts.FindIndex(x =>
                    x.Name.StartsWith(c_PropertiesFolder, StringComparison.Ordinal)
                    && x.Name.EndsWith(c_PropertiesExtension, StringComparison.Ordinal));
                int relationships = parts.FindIndex(x => x.Name == c_Relationships);

                if (properties < 0 || relationships < 0)
                {
                    return;
                }

                string oldName = parts[properties].Name;
                parts[properties] = (c_PropertiesName, parts[properties].Content);

                // The random identifiers are numbered in the order in which they come, from the first number that no
                // relationship has.
                string named = Encoding.UTF8.GetString(parts[relationships].Content).Replace(oldName, c_PropertiesName, StringComparison.Ordinal);
                int next = 1;
                string numbered = s_RandomIdentifier.Replace(named, _ =>
                {
                    string identifier;
                    do
                    {
                        next++;
                        identifier = $@"rId{next}";
                    }
                    while (named.Contains($@"Id=""{identifier}""", StringComparison.Ordinal));

                    return identifier;
                });
                parts[relationships] = (c_Relationships, Encoding.UTF8.GetBytes(numbered));
            }
        }
    }

    /// <summary>
    /// A sheet of an <see cref="XlsxWorkbookWriter"/>. Each call makes the cell that it names: a cell is written once.
    /// </summary>
    internal sealed class XlsxSheetWriter
    {
        // The largest number that is kept to 15 digits and is still a number: one more digit in the last place, and the
        // number is more than a number can hold, which the library that reads the workbook does not take.
        private const double c_LargestNumber = 1.79769313486231E+308;

        private readonly IXLWorksheet m_Sheet;
        private readonly XlsxDateStyles? m_DateStyles;

        internal XlsxSheetWriter(
            IXLWorksheet sheet,
            XlsxDateStyles? dateStyles)
        {
            m_Sheet = sheet;
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
        public void Text(int row, int column, string? text, XlsxCellStyle style = XlsxCellStyle.Plain)
        {
            IXLCell cell = Create(row, column, style);

            // The library takes an apostrophe at the start of a text for the mark of a text that looks like something else (a
            // number, say), and removes it. An apostrophe in front of that one is the one that stays.
            cell.Value = text is not null && text.StartsWith('\'')
                ? string.Concat("'", text)
                : text;
        }

        public void Number(int row, int column, double number, XlsxCellStyle style = XlsxCellStyle.Plain)
        {
            IXLCell cell = Create(row, column, style);

            // A cell can hold an error where it cannot hold the number.
            if (double.IsNaN(number))
            {
                cell.Value = XLError.NumberInvalid;
            }
            else if (double.IsInfinity(number))
            {
                cell.Value = XLError.DivisionByZero;
            }
            else if (Math.Abs(number) > c_LargestNumber)
            {
                cell.Value = XLError.NumberInvalid;
            }
            else
            {
                cell.Value = number;
            }
        }

        public void Boolean(int row, int column, bool value, XlsxCellStyle style = XlsxCellStyle.Plain) =>
            Create(row, column, style).Value = value;

        /// <summary>
        /// A date. The cell has the format of a date, and so the style is <see cref="XlsxCellStyle.Date"/> or
        /// <see cref="XlsxCellStyle.TitleDate"/>.
        /// </summary>
        public void Date(int row, int column, DateTime date, XlsxCellStyle style = XlsxCellStyle.Date) =>
            Create(row, column, style).Value = date;

        // The cells are counted from 1 in the library.
        private IXLCell Create(int row, int column, XlsxCellStyle style)
        {
            IXLCell cell = m_Sheet.Cell(row + 1, column + 1);

            switch (style)
            {
                case XlsxCellStyle.Plain:
                    break;
                case XlsxCellStyle.Title:
                    Title(cell);
                    break;
                case XlsxCellStyle.Date:
                    cell.Style.DateFormat.Format = (m_DateStyles ?? throw new InvalidOperationException()).DateFormat;
                    break;
                case XlsxCellStyle.TitleDate:
                    XlsxDateStyles dateStyles = m_DateStyles ?? throw new InvalidOperationException();
                    cell.Style.DateFormat.Format = dateStyles.HasTitleDate ? dateStyles.DateFormat : throw new InvalidOperationException();
                    Title(cell);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(style), style, null);
            }

            return cell;
        }

        private static void Title(IXLCell cell)
        {
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 12;
        }
    }
}
