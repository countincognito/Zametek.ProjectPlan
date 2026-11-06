using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    public enum XlsxCellKind
    {
        Blank,
        Text,
        Number,
        Boolean,
        Date,
        Error,
    }

    /// <summary>
    /// One cell of a worksheet, as the package holds it: what kind of value it is, the value, and the look that the
    /// exporter gives it (the format of a number, bold, the size of the font).
    /// </summary>
    internal sealed record XlsxCell(
        int Row,
        int Column,
        XlsxCellKind Kind,
        string Text,
        double Number,
        bool Boolean,
        string FormatCode,
        bool Bold,
        double FontSize,
        string? Formula)
    {
        public string Ref => $@"{XlsxPackageReader.ColumnLetters(Column)}{Row}";

        // A line that says all that a reader of the workbook can see of the cell. A number is held to 15 significant
        // digits: that is what Excel shows, and what a workbook can be trusted to hold. The format of a date is left out when
        // the caller says so: it is the short date of the culture that the exporter first met, and so it is not the same on
        // every machine.
        public string Describe(bool dateFormats = true)
        {
            string value = Kind switch
            {
                XlsxCellKind.Number or XlsxCellKind.Date => Number.ToString(@"G15", CultureInfo.InvariantCulture),
                XlsxCellKind.Boolean => Boolean ? @"TRUE" : @"FALSE",
                _ => Text,
            };
            string format = FormatCode is @"General" or @"" || (Kind == XlsxCellKind.Date && !dateFormats) ? string.Empty : $@" [{FormatCode}]";
            string look = Bold || Math.Abs(FontSize - 11.0) > 0.001 ? $@" ({(Bold ? @"bold " : string.Empty)}{FontSize.ToString(@"G15", CultureInfo.InvariantCulture)}pt)" : string.Empty;
            string formula = Formula is null ? string.Empty : $@" ={Formula}";
            return $@"{Ref} {Kind} {value.Replace("\n", "\\n", StringComparison.Ordinal)}{format}{look}{formula}";
        }
    }

    internal sealed class XlsxSheet
    {
        private readonly Dictionary<(int Row, int Column), XlsxCell> m_Lookup;

        public XlsxSheet(string name, IReadOnlyList<XlsxCell> cells)
        {
            Name = name;
            Cells = cells;
            m_Lookup = cells.ToDictionary(x => (x.Row, x.Column));
        }

        public string Name { get; }

        // In row order, and column order within a row. A cell that the package does not hold is not there.
        public IReadOnlyList<XlsxCell> Cells { get; }

        public int LastRow => Cells.Select(x => x.Row).DefaultIfEmpty(0).Max();

        public int LastColumn => Cells.Select(x => x.Column).DefaultIfEmpty(0).Max();

        public XlsxCell? Cell(int row, int column) => m_Lookup.GetValueOrDefault((row, column));

        public XlsxCell? Cell(string reference)
        {
            Match match = Regex.Match(reference, @"^([A-Z]+)(\d+)$");
            return Cell(int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), XlsxPackageReader.ColumnNumber(match.Groups[1].Value));
        }

        // The titles of the first row, in the order of the columns, and as text whatever kind each cell is.
        public IReadOnlyList<string> Header() =>
            [.. Enumerable.Range(1, LastColumn).Select(x => Cell(1, x)?.Text ?? string.Empty)];

        // A cell that holds no value, or only an empty text, says nothing: one library writes the empty text and another leaves
        // the cell out, and a reader of the workbook cannot tell them apart.
        public IEnumerable<string> Describe(bool dateFormats = true) =>
            Cells.Where(x => x.Kind != XlsxCellKind.Blank && !(x.Kind == XlsxCellKind.Text && x.Text.Length == 0)).Select(x => x.Describe(dateFormats));
    }

    internal sealed class XlsxBook
    {
        public XlsxBook(IReadOnlyList<XlsxSheet> sheets, DateTime? created, DateTime? modified)
        {
            Sheets = sheets;
            Created = created;
            Modified = modified;
        }

        public IReadOnlyList<XlsxSheet> Sheets { get; }

        public IReadOnlyList<string> SheetNames => [.. Sheets.Select(x => x.Name)];

        // The time that the properties of the workbook say it was created, in UTC.
        public DateTime? Created { get; }

        // The time that the properties of the workbook say it was last modified, in UTC.
        public DateTime? Modified { get; }

        // As a spreadsheet looks a sheet up: not by case.
        public XlsxSheet? Sheet(string name) =>
            Sheets.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

        // Every sheet, in order, with every cell that holds a value: what two workbooks must share to be the same workbook.
        public IReadOnlyList<string> Describe(bool dateFormats = true) =>
            [.. Sheets.SelectMany(x => x.Describe(dateFormats).Prepend($@"## {x.Name}"))];

        // The first differences between two descriptions, for a message.
        public static string Difference(IReadOnlyList<string> expected, IReadOnlyList<string> actual, int limit = 12)
        {
            var differences = new List<string>();
            int count = Math.Max(expected.Count, actual.Count);

            for (int i = 0; i < count && differences.Count < limit; i++)
            {
                string? e = i < expected.Count ? expected[i] : null;
                string? a = i < actual.Count ? actual[i] : null;

                if (e != a)
                {
                    differences.Add($@"line {i + 1}: expected [{e}] but was [{a}]");
                }
            }

            return differences.Count == 0
                ? string.Empty
                : $@"{expected.Count} lines expected and {actual.Count} lines found; {string.Join(@" // ", differences)}";
        }
    }

    /// <summary>
    /// Reads a workbook by its XML alone, so that a test says what a workbook holds without naming the spreadsheet library
    /// that wrote it, or the one that reads it.
    /// </summary>
    internal static class XlsxPackageReader
    {
        private static readonly XNamespace s_Main = @"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace s_Relationships = @"http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace s_Package = @"http://schemas.openxmlformats.org/package/2006/relationships";
        private static readonly XNamespace s_DcTerms = @"http://purl.org/dc/terms/";

        // The formats that Excel numbers, for those that a workbook can say by their number alone.
        private static readonly Dictionary<int, string> s_BuiltInFormats = new()
        {
            [0] = @"General",
            [1] = @"0",
            [2] = @"0.00",
            [3] = @"#,##0",
            [4] = @"#,##0.00",
            [9] = @"0%",
            [10] = @"0.00%",
            [11] = @"0.00E+00",
            [14] = @"m/d/yyyy",
            [15] = @"d-mmm-yy",
            [16] = @"d-mmm",
            [17] = @"mmm-yy",
            [18] = @"h:mm AM/PM",
            [19] = @"h:mm:ss AM/PM",
            [20] = @"h:mm",
            [21] = @"h:mm:ss",
            [22] = @"m/d/yyyy h:mm",
            [45] = @"mm:ss",
            [46] = @"[h]:mm:ss",
            [47] = @"mmss.0",
            [49] = @"@",
        };

        public static string ColumnLetters(int column)
        {
            string letters = string.Empty;

            for (int n = column; n > 0; n = (n - 1) / 26)
            {
                letters = (char)('A' + ((n - 1) % 26)) + letters;
            }

            return letters;
        }

        public static int ColumnNumber(string letters) =>
            letters.Aggregate(0, (total, c) => (total * 26) + (c - 'A' + 1));

        public static XlsxBook Read(byte[] package)
        {
            ArgumentNullException.ThrowIfNull(package);
            using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);

            XDocument? Part(string name)
            {
                ZipArchiveEntry? entry = archive.GetEntry(name);
                if (entry is null)
                {
                    return null;
                }
                using Stream stream = entry.Open();
                return XDocument.Load(stream);
            }

            XDocument workbook = Part(@"xl/workbook.xml") ?? throw new InvalidDataException(@"The package has no workbook part.");
            XDocument workbookRelationships = Part(@"xl/_rels/workbook.xml.rels") ?? throw new InvalidDataException(@"The package has no relationships for the workbook.");
            Dictionary<string, string> targets = workbookRelationships.Descendants(s_Package + @"Relationship")
                .ToDictionary(x => (string)x.Attribute(@"Id")!, x => (string)x.Attribute(@"Target")!);

            List<string> sharedStrings = [];
            XDocument? shared = Part(@"xl/sharedStrings.xml");
            if (shared is not null)
            {
                sharedStrings = [.. shared.Descendants(s_Main + @"si").Select(TextOf)];
            }

            Styles styles = Styles.From(Part(@"xl/styles.xml"));
            var sheets = new List<XlsxSheet>();

            foreach (XElement sheet in workbook.Descendants(s_Main + @"sheet"))
            {
                string name = (string)sheet.Attribute(@"name")!;
                string target = targets[(string)sheet.Attribute(s_Relationships + @"id")!];
                string path = target.StartsWith('/') ? target[1..] : $@"xl/{target}";
                XDocument worksheet = Part(path) ?? throw new InvalidDataException($@"The package has no part {path}.");
                sheets.Add(new XlsxSheet(name, ReadCells(worksheet, sharedStrings, styles)));
            }

            return new XlsxBook(sheets, ReadProperty(archive, @"created"), ReadProperty(archive, @"modified"));
        }

        private static string TextOf(XElement element) =>
            string.Concat(element.Descendants(s_Main + @"t").Where(x => !x.Ancestors(s_Main + @"rPh").Any()).Select(x => x.Value));

        private static List<XlsxCell> ReadCells(XDocument worksheet, List<string> sharedStrings, Styles styles)
        {
            var cells = new List<XlsxCell>();

            foreach (XElement row in worksheet.Descendants(s_Main + @"row"))
            {
                int rowNumber = (int?)row.Attribute(@"r") ?? (cells.Count == 0 ? 1 : cells[^1].Row + 1);
                int column = 0;

                foreach (XElement c in row.Elements(s_Main + @"c"))
                {
                    string? reference = (string?)c.Attribute(@"r");
                    column = reference is null
                        ? column + 1
                        : ColumnNumber(Regex.Match(reference, @"^[A-Z]+").Value);

                    string type = (string?)c.Attribute(@"t") ?? @"n";
                    int styleIndex = (int?)c.Attribute(@"s") ?? 0;
                    string? value = (string?)c.Element(s_Main + @"v");
                    string? formula = (string?)c.Element(s_Main + @"f");
                    (string formatCode, bool isDate, bool bold, double size) = styles.Of(styleIndex);

                    XlsxCellKind kind;
                    string text = string.Empty;
                    double number = 0.0;
                    bool flag = false;

                    switch (type)
                    {
                        case @"s" when value is not null:
                            kind = XlsxCellKind.Text;
                            text = sharedStrings[int.Parse(value, CultureInfo.InvariantCulture)];
                            break;
                        case @"inlineStr":
                            kind = XlsxCellKind.Text;
                            text = c.Element(s_Main + @"is") is XElement inline ? TextOf(inline) : string.Empty;
                            break;
                        case @"str" when value is not null:
                            kind = XlsxCellKind.Text;
                            text = value;
                            break;
                        case @"b" when value is not null:
                            kind = XlsxCellKind.Boolean;
                            flag = value == @"1";
                            break;
                        case @"e" when value is not null:
                            kind = XlsxCellKind.Error;
                            text = value;
                            break;
                        case @"d" when value is not null:
                            kind = XlsxCellKind.Date;
                            text = value;
                            number = DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToOADate();
                            break;
                        case @"n" when value is not null:
                            number = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
                            kind = isDate ? XlsxCellKind.Date : XlsxCellKind.Number;
                            text = value;
                            break;
                        default:
                            kind = XlsxCellKind.Blank;
                            break;
                    }

                    cells.Add(new XlsxCell(rowNumber, column, kind, text, number, flag, formatCode, bold, size, formula));
                }
            }

            return [.. cells.OrderBy(x => x.Row).ThenBy(x => x.Column)];
        }

        private static DateTime? ReadProperty(ZipArchive archive, string name)
        {
            // The properties of a workbook are in a part that _rels/.rels names. One library calls it docProps/core.xml,
            // and another gives it a name of its own.
            ZipArchiveEntry? rels = archive.GetEntry(@"_rels/.rels");
            if (rels is null)
            {
                return null;
            }

            XDocument document;
            using (Stream stream = rels.Open())
            {
                document = XDocument.Load(stream);
            }

            string? target = document.Descendants(s_Package + @"Relationship")
                .FirstOrDefault(x => ((string?)x.Attribute(@"Type"))?.EndsWith(@"/core-properties", StringComparison.Ordinal) == true)
                ?.Attribute(@"Target")?.Value;
            ZipArchiveEntry? part = target is null ? null : archive.GetEntry(target.TrimStart('/'));
            if (part is null)
            {
                return null;
            }

            using Stream properties = part.Open();
            string? time = XDocument.Load(properties).Descendants(s_DcTerms + name).FirstOrDefault()?.Value;
            return time is null
                ? null
                : DateTime.Parse(time, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        }

        private sealed class Styles
        {
            private readonly List<(string FormatCode, bool IsDate, bool Bold, double Size)> m_CellFormats = [];

            public static Styles From(XDocument? document)
            {
                var styles = new Styles();
                if (document is null)
                {
                    return styles;
                }

                Dictionary<int, string> custom = document.Descendants(s_Main + @"numFmt")
                    .ToDictionary(x => (int)x.Attribute(@"numFmtId")!, x => (string)x.Attribute(@"formatCode")!);
                List<(bool Bold, double Size)> fonts = [.. document.Descendants(s_Main + @"fonts").Elements(s_Main + @"font")
                    .Select(x => (x.Element(s_Main + @"b") is { } b && ((string?)b.Attribute(@"val") ?? @"1") is not (@"0" or @"false"),
                        (double?)x.Element(s_Main + @"sz")?.Attribute(@"val") ?? 11.0))];

                foreach (XElement xf in document.Descendants(s_Main + @"cellXfs").Elements(s_Main + @"xf"))
                {
                    int formatId = (int?)xf.Attribute(@"numFmtId") ?? 0;
                    int fontId = (int?)xf.Attribute(@"fontId") ?? 0;
                    string code = custom.TryGetValue(formatId, out string? c)
                        ? c
                        : s_BuiltInFormats.GetValueOrDefault(formatId, $@"built-in {formatId}");
                    bool isDate = formatId is (>= 14 and <= 22) or (>= 45 and <= 47) || IsDateCode(code);
                    (bool bold, double size) = fontId < fonts.Count ? fonts[fontId] : (false, 11.0);
                    styles.m_CellFormats.Add((code, isDate, bold, size));
                }

                return styles;
            }

            public (string FormatCode, bool IsDate, bool Bold, double Size) Of(int index) =>
                index < m_CellFormats.Count ? m_CellFormats[index] : (@"General", false, false, 11.0);

            // A format is for dates if, outside quotes, brackets and escapes, it has a day, a month, a year, an hour or a second.
            private static bool IsDateCode(string code)
            {
                if (code is @"General" or @"@")
                {
                    return false;
                }

                string stripped = Regex.Replace(code, @"""[^""]*""|\[[^\]]*\]|\\.|_.|\*.", string.Empty);
                return Regex.IsMatch(stripped, @"[dmyhsDMYHS]") && !Regex.IsMatch(stripped, @"^[#0,.%\s]*(E\+0+)?$");
            }
        }
    }
}
