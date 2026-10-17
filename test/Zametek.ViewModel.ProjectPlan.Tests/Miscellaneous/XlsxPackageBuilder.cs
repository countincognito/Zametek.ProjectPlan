using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A cell that a test puts into a workbook. The kinds are the ones that a workbook that a person made in Excel can hold,
    /// and that the importer meets: text (as a shared string, as Excel writes it, or in the cell), numbers, truth values,
    /// dates (a number with the format of a date), formulas with the result that Excel kept, errors, and cells that only
    /// have a look.
    /// </summary>
    internal sealed record XlsxCellSpec(
        XlsxCellKind Kind,
        string Text = "",
        double Number = 0.0,
        bool Boolean = false,
        string? Formula = null,
        bool Inline = false,
        bool Bold = false)
    {
        public static XlsxCellSpec TextOf(string text) => new(XlsxCellKind.Text, Text: text);

        public static XlsxCellSpec InlineText(string text) => new(XlsxCellKind.Text, Text: text, Inline: true);

        public static XlsxCellSpec NumberOf(double number) => new(XlsxCellKind.Number, Number: number);

        public static XlsxCellSpec BooleanOf(bool value) => new(XlsxCellKind.Boolean, Boolean: value);

        public static XlsxCellSpec DateOf(DateTime date) => new(XlsxCellKind.Date, Number: date.ToOADate());

        public static XlsxCellSpec ErrorOf(string error) => new(XlsxCellKind.Error, Text: error);

        // A formula, and the number that the file keeps as its result.
        public static XlsxCellSpec FormulaNumber(string formula, double result) => new(XlsxCellKind.Number, Number: result, Formula: formula);

        // A formula, and the text that the file keeps as its result.
        public static XlsxCellSpec FormulaText(string formula, string result) => new(XlsxCellKind.Text, Text: result, Formula: formula);

        // A cell that has a look and no value, as Excel keeps one that a person formatted and left empty.
        public static XlsxCellSpec StyledBlank() => new(XlsxCellKind.Blank, Bold: true);

        public static XlsxCellSpec TitleOf(string text) => new(XlsxCellKind.Text, Text: text, Bold: true);

        // What a test says as an object: a string is text, a number is a number, a flag is a truth value, a date is a date,
        // and nothing is a cell that is not there.
        public static XlsxCellSpec? From(object? value) =>
            value switch
            {
                null => null,
                XlsxCellSpec spec => spec,
                string text => TextOf(text),
                bool flag => BooleanOf(flag),
                int number => NumberOf(number),
                double number => NumberOf(number),
                DateTime date => DateOf(date),
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
            };
    }

    /// <summary>
    /// The rows of one worksheet of a workbook that a test makes.
    /// </summary>
    internal sealed class XlsxSheetSpec
    {
        private readonly SortedDictionary<int, SortedDictionary<int, XlsxCellSpec>> m_Rows = [];

        public XlsxSheetSpec(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public IEnumerable<(int Row, IEnumerable<(int Column, XlsxCellSpec Cell)> Cells)> Rows =>
            m_Rows.Select(x => (x.Key, x.Value.Select(y => (y.Key, y.Value))));

        // A row at the given number, with the cells at the given columns ("A", "B"...). A column that is not named has no cell.
        public XlsxSheetSpec Row(int row, params (string Column, XlsxCellSpec Cell)[] cells)
        {
            if (!m_Rows.TryGetValue(row, out SortedDictionary<int, XlsxCellSpec>? existing))
            {
                existing = [];
                m_Rows[row] = existing;
            }

            foreach ((string column, XlsxCellSpec cell) in cells)
            {
                existing[XlsxPackageReader.ColumnNumber(column)] = cell;
            }

            return this;
        }

        // The titles in row 1, in bold as the exporter writes them, and a row for each array of values under them, from column A.
        public XlsxSheetSpec Table(IEnumerable<string> header, params object?[][] rows)
        {
            Row(1, [.. header.Select((x, i) => (XlsxPackageReader.ColumnLetters(i + 1), XlsxCellSpec.TitleOf(x)))]);

            for (int i = 0; i < rows.Length; i++)
            {
                Row(
                    i + 2,
                    [.. rows[i]
                        .Select((x, j) => (Column: XlsxPackageReader.ColumnLetters(j + 1), Cell: XlsxCellSpec.From(x)))
                        .Where(x => x.Cell is not null)
                        .Select(x => (x.Column, x.Cell!))]);
            }

            return this;
        }
    }

    /// <summary>
    /// Makes a workbook from its XML alone, as the files of Excel are made: a styles part, the strings in a shared table, rows
    /// and cells that are only the ones that hold something. A test that needs a workbook that no exporter writes, such as a
    /// row that has no cell in its first column, makes it with this.
    /// </summary>
    internal sealed class XlsxPackageBuilder
    {
        // The formats of the styles that a cell can have: 0 is General, 1 is bold, and 2 is a date.
        private const string c_DateFormat = @"dd/mm/yyyy";

        private readonly List<XlsxSheetSpec> m_Sheets = [];

        public XlsxPackageBuilder Sheet(string name, Action<XlsxSheetSpec> fill)
        {
            var sheet = new XlsxSheetSpec(name);
            fill(sheet);
            m_Sheets.Add(sheet);
            return this;
        }

        public byte[] Build()
        {
            var shared = new List<string>();
            var sharedLookup = new Dictionary<string, int>();

            int SharedIndex(string text)
            {
                if (!sharedLookup.TryGetValue(text, out int index))
                {
                    index = shared.Count;
                    shared.Add(text);
                    sharedLookup[text] = index;
                }
                return index;
            }

            var worksheets = new List<string>();

            foreach (XlsxSheetSpec sheet in m_Sheets)
            {
                var xml = new StringBuilder();
                xml.Append(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
                xml.Append(@"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""><sheetData>");

                foreach ((int row, IEnumerable<(int Column, XlsxCellSpec Cell)> cells) in sheet.Rows)
                {
                    xml.Append(CultureInfo.InvariantCulture, $@"<row r=""{row}"">");

                    foreach ((int column, XlsxCellSpec cell) in cells)
                    {
                        xml.Append(CellXml($@"{XlsxPackageReader.ColumnLetters(column)}{row}", cell, SharedIndex));
                    }

                    xml.Append(@"</row>");
                }

                xml.Append(@"</sheetData></worksheet>");
                worksheets.Add(xml.ToString());
            }

            using var package = new MemoryStream();
            using (var archive = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
            {
                void Add(string name, string content)
                {
                    ZipArchiveEntry entry = archive.CreateEntry(name);
                    using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                    writer.Write(content);
                }

                var contentTypes = new StringBuilder(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?><Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types""><Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/><Default Extension=""xml"" ContentType=""application/xml""/><Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/><Override PartName=""/xl/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml""/><Override PartName=""/xl/sharedStrings.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml""/>");
                var workbookSheets = new StringBuilder();
                var relationships = new StringBuilder(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?><Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">");

                for (int i = 0; i < m_Sheets.Count; i++)
                {
                    int number = i + 1;
                    contentTypes.Append(CultureInfo.InvariantCulture, $@"<Override PartName=""/xl/worksheets/sheet{number}.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>");
                    workbookSheets.Append(CultureInfo.InvariantCulture, $@"<sheet name=""{SecurityElement.Escape(m_Sheets[i].Name)}"" sheetId=""{number}"" r:id=""rId{number}""/>");
                    relationships.Append(CultureInfo.InvariantCulture, $@"<Relationship Id=""rId{number}"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet{number}.xml""/>");
                    Add($@"xl/worksheets/sheet{number}.xml", worksheets[i]);
                }

                relationships.Append(CultureInfo.InvariantCulture, $@"<Relationship Id=""rId{m_Sheets.Count + 1}"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>");
                relationships.Append(CultureInfo.InvariantCulture, $@"<Relationship Id=""rId{m_Sheets.Count + 2}"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings"" Target=""sharedStrings.xml""/>");
                relationships.Append(@"</Relationships>");
                contentTypes.Append(@"</Types>");

                Add(@"[Content_Types].xml", contentTypes.ToString());
                Add(@"_rels/.rels", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?><Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships""><Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/></Relationships>");
                Add(@"xl/workbook.xml", $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?><workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships""><sheets>{workbookSheets}</sheets></workbook>");
                Add(@"xl/_rels/workbook.xml.rels", relationships.ToString());
                Add(
                    @"xl/styles.xml",
                    $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?><styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""><numFmts count=""1""><numFmt numFmtId=""164"" formatCode=""{c_DateFormat}""/></numFmts><fonts count=""2""><font><sz val=""11""/><name val=""Calibri""/></font><font><b/><sz val=""12""/><name val=""Calibri""/></font></fonts><fills count=""2""><fill><patternFill patternType=""none""/></fill><fill><patternFill patternType=""gray125""/></fill></fills><borders count=""1""><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count=""1""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0""/></cellStyleXfs><cellXfs count=""3""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/><xf numFmtId=""0"" fontId=""1"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1""/><xf numFmtId=""164"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0"" applyNumberFormat=""1""/></cellXfs></styleSheet>");
                Add(
                    @"xl/sharedStrings.xml",
                    $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?><sst xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" count=""{shared.Count}"" uniqueCount=""{shared.Count}"">{string.Concat(shared.Select(x => $@"<si><t xml:space=""preserve"">{SecurityElement.Escape(x)}</t></si>"))}</sst>");
            }

            return package.ToArray();
        }

        private static string CellXml(string reference, XlsxCellSpec cell, Func<string, int> sharedIndex)
        {
            string number = cell.Number.ToString(@"R", CultureInfo.InvariantCulture);
            string formula = cell.Formula is null ? string.Empty : $@"<f>{SecurityElement.Escape(cell.Formula)}</f>";

            return cell.Kind switch
            {
                XlsxCellKind.Text when cell.Formula is not null =>
                    $@"<c r=""{reference}"" t=""str"">{formula}<v>{SecurityElement.Escape(cell.Text)}</v></c>",
                XlsxCellKind.Text when cell.Inline =>
                    $@"<c r=""{reference}"" t=""inlineStr"" s=""{(cell.Bold ? 1 : 0)}""><is><t xml:space=""preserve"">{SecurityElement.Escape(cell.Text)}</t></is></c>",
                XlsxCellKind.Text =>
                    $@"<c r=""{reference}"" t=""s"" s=""{(cell.Bold ? 1 : 0)}""><v>{sharedIndex(cell.Text)}</v></c>",
                XlsxCellKind.Number =>
                    $@"<c r=""{reference}"">{formula}<v>{number}</v></c>",
                XlsxCellKind.Date =>
                    $@"<c r=""{reference}"" s=""2""><v>{number}</v></c>",
                XlsxCellKind.Boolean =>
                    $@"<c r=""{reference}"" t=""b""><v>{(cell.Boolean ? 1 : 0)}</v></c>",
                XlsxCellKind.Error =>
                    $@"<c r=""{reference}"" t=""e""><v>{SecurityElement.Escape(cell.Text)}</v></c>",
                XlsxCellKind.Blank =>
                    $@"<c r=""{reference}"" s=""{(cell.Bold ? 1 : 0)}""/>",
                _ => throw new ArgumentOutOfRangeException(nameof(cell), cell, null),
            };
        }
    }
}
