using Shouldly;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Xunit;
using static Zametek.ViewModel.ProjectPlan.Tests.XlsxCellSpec;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The tools that the tests of the Excel workbooks use to make, change and read a workbook by its XML alone. Those tests
    /// say what a workbook holds without naming the library that wrote or reads it, so these tools must say it truly.
    /// </summary>
    public class XlsxPackageHelperTests
    {
        private static byte[] Book(Action<XlsxSheetSpec> fill) => new XlsxPackageBuilder().Sheet(@"S", fill).Build();

        [Fact]
        public void Read_Given_ABuiltWorkbook_Then_EachKindOfCellIsReadAsItWasBuilt()
        {
            byte[] book = Book(s => s.Row(
                1,
                ("A", TextOf(@"shared")),
                ("B", InlineText(@"inline")),
                ("C", NumberOf(1.5)),
                ("D", BooleanOf(true)),
                ("E", DateOf(new DateTime(2026, 10, 17))),
                ("F", ErrorOf(@"#N/A")),
                ("G", FormulaNumber(@"1+1", 2.0)),
                ("H", FormulaText(@"""a""&""b""", @"ab")),
                ("J", StyledBlank()),
                ("K", TitleOf(@"title"))));

            XlsxSheet sheet = XlsxPackageReader.Read(book).Sheet(@"S")!;

            sheet.Cell(@"A1")!.ShouldSatisfyAllConditions(x => x.Kind.ShouldBe(XlsxCellKind.Text), x => x.Text.ShouldBe(@"shared"));
            sheet.Cell(@"B1")!.ShouldSatisfyAllConditions(x => x.Kind.ShouldBe(XlsxCellKind.Text), x => x.Text.ShouldBe(@"inline"));
            sheet.Cell(@"C1")!.ShouldSatisfyAllConditions(x => x.Kind.ShouldBe(XlsxCellKind.Number), x => x.Number.ShouldBe(1.5));
            sheet.Cell(@"D1")!.ShouldSatisfyAllConditions(x => x.Kind.ShouldBe(XlsxCellKind.Boolean), x => x.Boolean.ShouldBeTrue());
            sheet.Cell(@"E1")!.ShouldSatisfyAllConditions(x => x.Kind.ShouldBe(XlsxCellKind.Date), x => x.Number.ShouldBe(new DateTime(2026, 10, 17).ToOADate()), x => x.FormatCode.ShouldBe(@"dd/mm/yyyy"));
            sheet.Cell(@"F1")!.ShouldSatisfyAllConditions(x => x.Kind.ShouldBe(XlsxCellKind.Error), x => x.Text.ShouldBe(@"#N/A"));
            sheet.Cell(@"G1")!.ShouldSatisfyAllConditions(x => x.Kind.ShouldBe(XlsxCellKind.Number), x => x.Number.ShouldBe(2.0), x => x.Formula.ShouldBe(@"1+1"));
            sheet.Cell(@"H1")!.ShouldSatisfyAllConditions(x => x.Kind.ShouldBe(XlsxCellKind.Text), x => x.Text.ShouldBe(@"ab"), x => x.Formula.ShouldBe(@"""a""&""b"""));
            sheet.Cell(@"J1")!.Kind.ShouldBe(XlsxCellKind.Blank);
            sheet.Cell(@"K1")!.ShouldSatisfyAllConditions(x => x.Bold.ShouldBeTrue(), x => x.FontSize.ShouldBe(12.0));
            sheet.Cell(@"I1").ShouldBeNull();
        }

        [Fact]
        public void Read_Given_ARowWithAGapAndARowThatIsMissing_Then_OnlyTheCellsThatAreThereAreRead()
        {
            byte[] book = Book(s => s
                .Row(1, ("A", TextOf(@"a")), ("C", TextOf(@"c")))
                .Row(4, ("B", NumberOf(2.0))));

            XlsxSheet sheet = XlsxPackageReader.Read(book).Sheet(@"S")!;

            sheet.Cells.Select(x => x.Ref).ShouldBe([@"A1", @"C1", @"B4"]);
            sheet.LastRow.ShouldBe(4);
            sheet.LastColumn.ShouldBe(3);
            sheet.Header().ShouldBe([@"a", string.Empty, @"c"]);
        }

        [Fact]
        public void Describe_Given_TwoWorkbooksThatHoldTheSameValuesInOtherWays_Then_TheyAreDescribedAlike()
        {
            // Text in the table of shared strings, as Excel writes it, and text in the cell; and a number that is held to the
            // digits that a workbook can be trusted to hold.
            byte[] shared = Book(s => s.Row(1, ("A", TextOf(@"same")), ("B", NumberOf(0.1 + 0.2))));
            byte[] inline = Book(s => s.Row(1, ("A", InlineText(@"same")), ("B", NumberOf(0.3))));

            XlsxPackageReader.Read(shared).Describe().ShouldBe(XlsxPackageReader.Read(inline).Describe());
        }

        [Fact]
        public void Difference_Given_TwoDescriptions_Then_SaysWhereTheyDiffer()
        {
            XlsxBook.Difference([@"a", @"b"], [@"a", @"b"]).ShouldBeEmpty();
            XlsxBook.Difference([@"a", @"b"], [@"a", @"c", @"d"]).ShouldContain(@"line 2: expected [b] but was [c]");
        }

        [Fact]
        public void Edit_Given_ARemovedSheetAndATypedCell_Then_TheWorkbookHasWhatAPersonLeftInIt()
        {
            byte[] book = new XlsxPackageBuilder()
                .Sheet(@"Keep", s => s.Row(1, ("A", NumberOf(1.0)), ("B", NumberOf(2.0))))
                .Sheet(@"Drop", s => s.Row(1, ("A", TextOf(@"gone"))))
                .Build();

            byte[] edited = XlsxPackageEditor.Edit(book, e => e.RemoveSheet(@"Drop").SetText(@"Keep", @"A1", @"typed"));

            XlsxBook read = XlsxPackageReader.Read(edited);
            read.SheetNames.ShouldBe([@"Keep"]);
            read.Sheet(@"Keep")!.Cell(@"A1")!.ShouldSatisfyAllConditions(x => x.Kind.ShouldBe(XlsxCellKind.Text), x => x.Text.ShouldBe(@"typed"));
            read.Sheet(@"Keep")!.Cell(@"B1")!.Number.ShouldBe(2.0);
            new ZipArchive(new MemoryStream(edited), ZipArchiveMode.Read).Entries.Select(x => x.FullName).ShouldNotContain(@"xl/worksheets/sheet2.xml");
        }

        [Fact]
        public void Read_Given_TheWorkbookOfTheExporter_Then_ItsSheetsAndItsTitlesAreThere()
        {
            var clock = new FixedTimeProvider(new DateTimeOffset(2001, 2, 3, 4, 5, 6, TimeSpan.Zero), TimeZoneInfo.Utc);
            using var stream = new MemoryStream();
            new XlsxScenarioFileExporter(new DateTimeCalculator(clock))
                .ExportProjectScenarioXlsxFile(CoreViewModelFixture.CreateProjectScenario(3), new Zametek.Common.ProjectPlan.ResourceSeriesSetModel(), new Zametek.Common.ProjectPlan.TrackingSeriesSetModel(), showDates: false, stream);

            XlsxBook book = XlsxPackageReader.Read(stream.ToArray());

            book.SheetNames.ShouldContain(Resource.ProjectPlan.Reporting.Reporting_WorksheetActivities);
            book.Created.ShouldBe(new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc));
            XlsxCell title = book.Sheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetActivities)!.Cell(@"A1")!;
            title.ShouldSatisfyAllConditions(x => x.Kind.ShouldBe(XlsxCellKind.Text), x => x.Bold.ShouldBeTrue(), x => x.FontSize.ShouldBe(12.0));
        }
    }
}
