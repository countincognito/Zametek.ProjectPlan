using Shouldly;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// What the writer of workbooks makes of each call, seen as a person who opens the workbook sees it: the kind of each value,
    /// its look, and the sheets in their order. The writer is the one place of the exporter that knows the library that writes the
    /// file, and so these tests say what a library that replaces it must do.
    /// </summary>
    public class XlsxWorkbookWriterTests
    {
        private const string c_DateFormat = @"dd/mm/yyyy";

        private static readonly DateTimeOffset s_Now = new(2026, 10, 17, 12, 30, 0, TimeSpan.FromHours(2));

        private static XlsxBook Write(Action<XlsxWorkbookWriter> write)
        {
            var writer = new XlsxWorkbookWriter();
            write(writer);
            using var stream = new MemoryStream();
            writer.Save(stream, s_Now);
            return XlsxPackageReader.Read(stream.ToArray());
        }

        private static XlsxCell CellOf(XlsxBook book, string sheet, string reference) =>
            book.Sheet(sheet).ShouldNotBeNull().Cell(reference).ShouldNotBeNull();

        #region The kinds of value

        [Fact]
        public void Cells_Given_EachKindOfValue_Then_TheWorkbookHoldsAValueOfThatKind()
        {
            XlsxBook book = Write(workbook =>
            {
                XlsxSheetWriter sheet = workbook.AddSheet(@"Values", workbook.CreateDateStyles(c_DateFormat));
                sheet.Text(0, 0, @"a text");
                sheet.Number(0, 1, 2.5);
                sheet.Boolean(0, 2, true);
                sheet.Boolean(0, 3, false);
                sheet.Date(0, 4, new DateTime(2026, 3, 9));
                sheet.Blank(0, 5);
            });

            XlsxCell text = CellOf(book, @"Values", @"A1");
            (text.Kind, text.Text).ShouldBe((XlsxCellKind.Text, @"a text"));
            XlsxCell number = CellOf(book, @"Values", @"B1");
            (number.Kind, number.Number).ShouldBe((XlsxCellKind.Number, 2.5));
            CellOf(book, @"Values", @"C1").Boolean.ShouldBeTrue();
            CellOf(book, @"Values", @"D1").Kind.ShouldBe(XlsxCellKind.Boolean);
            CellOf(book, @"Values", @"D1").Boolean.ShouldBeFalse();
            XlsxCell date = CellOf(book, @"Values", @"E1");
            (date.Kind, date.FormatCode).ShouldBe((XlsxCellKind.Date, c_DateFormat));
            DateTime.FromOADate(date.Number).ShouldBe(new DateTime(2026, 3, 9));
        }

        // The seed of a simulation is text of up to 20 digits, and a text is held exactly.
        [Fact]
        public void Text_Given_AVeryLongTextOfDigits_Then_ItIsHeldExactly()
        {
            XlsxBook book = Write(workbook => workbook.AddSheet(@"Values").Text(0, 0, ulong.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));

            CellOf(book, @"Values", @"A1").Text.ShouldBe(@"18446744073709551615");
        }

        [Fact]
        public void Text_Given_NoText_Then_TheCellHasNoValue()
        {
            XlsxBook book = Write(workbook => workbook.AddSheet(@"Values").Text(0, 0, null));

            book.Sheet(@"Values").ShouldNotBeNull().Cells.Where(x => x.Kind != XlsxCellKind.Blank).ShouldBeEmpty();
        }

        // A number is held to the 15 significant digits that a workbook can be trusted to hold.
        [Theory]
        [InlineData(0.1 + 0.2)]
        [InlineData(1234567.891)]
        [InlineData(-0.000123456789012345)]
        [InlineData(1e300)]
        [InlineData(0.0)]
        public void Number_Given_ADouble_Then_ItIsHeldToFifteenSignificantDigits(double value)
        {
            XlsxBook book = Write(workbook => workbook.AddSheet(@"Values").Number(0, 0, value));

            double.Parse(CellOf(book, @"Values", @"A1").Number.ToString(@"G15", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture)
                .ShouldBe(double.Parse(value.ToString(@"G15", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture));
        }

        // A number that is not a number (and one without an end) is what a workbook holds as an error, and not as a value.
        [Theory]
        [InlineData(double.NaN, @"#NUM!")]
        [InlineData(double.PositiveInfinity, @"#DIV/0!")]
        [InlineData(double.NegativeInfinity, @"#DIV/0!")]
        public void Number_Given_ANumberWithNoValue_Then_TheCellHoldsAnErrorOfExcel(double value, string error)
        {
            XlsxBook book = Write(workbook => workbook.AddSheet(@"Values").Number(0, 0, value));

            XlsxCell cell = CellOf(book, @"Values", @"A1");
            (cell.Kind, cell.Text).ShouldBe((XlsxCellKind.Error, error));
        }

        #endregion

        #region Looks

        [Fact]
        public void Style_Given_ATitle_Then_TheCellIsBoldAtTwelvePoints()
        {
            XlsxBook book = Write(workbook =>
            {
                XlsxSheetWriter sheet = workbook.AddSheet(@"Looks");
                sheet.Text(0, 0, @"Title", XlsxCellStyle.Title);
                sheet.Number(0, 1, 7, XlsxCellStyle.Title);
                sheet.Text(1, 0, @"Plain");
            });

            (CellOf(book, @"Looks", @"A1").Bold, CellOf(book, @"Looks", @"A1").FontSize).ShouldBe((true, 12.0));
            (CellOf(book, @"Looks", @"B1").Bold, CellOf(book, @"Looks", @"B1").FontSize).ShouldBe((true, 12.0));
            CellOf(book, @"Looks", @"A2").Bold.ShouldBeFalse();
            CellOf(book, @"Looks", @"A2").FontSize.ShouldBe(11.0);
        }

        [Fact]
        public void Style_Given_ATitleThatIsADate_Then_TheCellIsBoldWithTheFormatOfADate()
        {
            XlsxBook book = Write(workbook =>
            {
                XlsxSheetWriter sheet = workbook.AddSheet(@"Looks", workbook.CreateDateStyles(c_DateFormat, includeTitleDate: true));
                sheet.Date(0, 0, new DateTime(2026, 3, 9), XlsxCellStyle.TitleDate);
                sheet.Date(0, 1, new DateTime(2026, 3, 9), XlsxCellStyle.Date);
            });

            XlsxCell title = CellOf(book, @"Looks", @"A1");
            (title.Kind, title.Bold, title.FontSize, title.FormatCode).ShouldBe((XlsxCellKind.Date, true, 12.0, c_DateFormat));
            XlsxCell plain = CellOf(book, @"Looks", @"B1");
            (plain.Kind, plain.Bold, plain.FormatCode).ShouldBe((XlsxCellKind.Date, false, c_DateFormat));
        }

        // A date cell takes the look of a date and nothing else, and so one that was a title in the same call is not.
        [Fact]
        public void Style_Given_ABlankCellWithALook_Then_TheLookIsHeld()
        {
            XlsxBook book = Write(workbook => workbook.AddSheet(@"Looks").Blank(0, 0, XlsxCellStyle.Title));

            XlsxCell cell = CellOf(book, @"Looks", @"A1");
            (cell.Kind, cell.Bold).ShouldBe((XlsxCellKind.Blank, true));
        }

        [Fact]
        public void Style_Given_ADateAndNoLooksOfDates_Then_ItIsAnError()
        {
            var writer = new XlsxWorkbookWriter();
            XlsxSheetWriter sheet = writer.AddSheet(@"Looks");

            Should.Throw<InvalidOperationException>(() => sheet.Date(0, 0, new DateTime(2026, 3, 9), XlsxCellStyle.Date));
        }

        [Fact]
        public void Style_Given_ATitleDateAndNoLookForOne_Then_ItIsAnError()
        {
            var writer = new XlsxWorkbookWriter();
            XlsxSheetWriter sheet = writer.AddSheet(@"Looks", writer.CreateDateStyles(c_DateFormat));

            Should.Throw<InvalidOperationException>(() => sheet.Date(0, 0, new DateTime(2026, 3, 9), XlsxCellStyle.TitleDate));
        }

        #endregion

        #region The shape of the workbook

        [Fact]
        public void AddSheet_Given_SeveralSheets_Then_TheyAreInTheOrderOfTheCalls()
        {
            XlsxBook book = Write(workbook =>
            {
                workbook.AddSheet(@"Second");
                workbook.AddSheet(@"First");
                workbook.AddSheet(@"Resource Tracker (2)");
            });

            book.SheetNames.ShouldBe([@"Second", @"First", @"Resource Tracker (2)"]);
        }

        [Fact]
        public void Cells_Given_CellsThatAreFarApart_Then_EachIsWhereItWasPut()
        {
            XlsxBook book = Write(workbook =>
            {
                XlsxSheetWriter sheet = workbook.AddSheet(@"Sparse");
                sheet.Text(0, 0, @"top left");
                sheet.Text(5, 27, @"far away");
                sheet.Number(3, 1, 9);
            });

            CellOf(book, @"Sparse", @"A1").Text.ShouldBe(@"top left");
            CellOf(book, @"Sparse", @"AB6").Text.ShouldBe(@"far away");
            CellOf(book, @"Sparse", @"B4").Number.ShouldBe(9.0);
            book.Sheet(@"Sparse").ShouldNotBeNull().Cells.Count(x => x.Kind != XlsxCellKind.Blank).ShouldBe(3);
        }

        // The cells of a row may be written in any order, and a row is made when its first cell is.
        [Fact]
        public void Cells_Given_RowsAndColumnsInAnyOrder_Then_EachIsWhereItWasPut()
        {
            XlsxBook book = Write(workbook =>
            {
                XlsxSheetWriter sheet = workbook.AddSheet(@"Order");
                sheet.Text(2, 2, @"C3");
                sheet.Text(0, 1, @"B1");
                sheet.Text(2, 0, @"A3");
                sheet.Text(0, 0, @"A1");
            });

            new[] { @"A1", @"B1", @"A3", @"C3" }.Select(x => CellOf(book, @"Order", x).Text).ShouldBe([@"A1", @"B1", @"A3", @"C3"]);
        }

        #endregion

        #region Saving

        [Fact]
        public void Save_Given_AStream_Then_TheStreamIsLeftOpenAndHoldsAWorkbook()
        {
            var writer = new XlsxWorkbookWriter();
            writer.AddSheet(@"Values").Text(0, 0, @"x");
            using var stream = new MemoryStream();

            writer.Save(stream, s_Now);

            stream.CanWrite.ShouldBeTrue();
            stream.Length.ShouldBeGreaterThan(0);
            XlsxPackageReader.Read(stream.ToArray()).SheetNames.ShouldBe([@"Values"]);
        }

        // The workbook says when it was made as the caller says, and not as the wall clock does, and so do the parts of its package.
        [Fact]
        public void Save_Given_ATime_Then_TheWorkbookAndEachPartOfItAreStampedWithIt()
        {
            var writer = new XlsxWorkbookWriter();
            writer.AddSheet(@"Values").Text(0, 0, @"x");
            using var stream = new MemoryStream();

            writer.Save(stream, s_Now);

            XlsxPackageReader.Read(stream.ToArray()).Created.ShouldBe(s_Now.UtcDateTime);
            using var archive = new ZipArchive(new MemoryStream(stream.ToArray()), ZipArchiveMode.Read);
            archive.Entries.ShouldNotBeEmpty();
            // A zip holds a time of the clock and no offset, and so it is the clock time that is compared.
            archive.Entries.ShouldAllBe(x => x.LastWriteTime.DateTime == s_Now.DateTime);
        }

        // A zip holds times from 1980 to 2107 only.
        [Theory]
        [InlineData(1970, 1980, 1, 1, 0, 0, 0)]
        [InlineData(2200, 2107, 12, 31, 23, 59, 58)]
        public void Save_Given_ATimeOutsideWhatAZipHolds_Then_ThePartsAreStampedWithTheNearerEnd(int year, int expectedYear, int month, int day, int hour, int minute, int second)
        {
            var writer = new XlsxWorkbookWriter();
            writer.AddSheet(@"Values").Text(0, 0, @"x");
            using var stream = new MemoryStream();

            writer.Save(stream, new DateTimeOffset(year, 6, 15, 8, 0, 0, TimeSpan.Zero));

            using var archive = new ZipArchive(new MemoryStream(stream.ToArray()), ZipArchiveMode.Read);
            archive.Entries.ShouldAllBe(x => x.LastWriteTime.DateTime == new DateTime(expectedYear, month, day, hour, minute, second));
        }

        // The same calls at the same time make the same workbook, byte for byte.
        [Fact]
        public void Save_Given_TheSameCallsAtTheSameTime_Then_TheWorkbookIsTheSame()
        {
            byte[] Make()
            {
                var writer = new XlsxWorkbookWriter();
                XlsxSheetWriter sheet = writer.AddSheet(@"Values", writer.CreateDateStyles(c_DateFormat, includeTitleDate: true));
                sheet.Text(0, 0, @"Title", XlsxCellStyle.Title);
                sheet.Date(1, 0, new DateTime(2026, 3, 9));
                sheet.Number(1, 1, 0.1 + 0.2);
                using var stream = new MemoryStream();
                writer.Save(stream, s_Now);
                return stream.ToArray();
            }

            Make().ShouldBe(Make());
        }

        #endregion
    }
}
