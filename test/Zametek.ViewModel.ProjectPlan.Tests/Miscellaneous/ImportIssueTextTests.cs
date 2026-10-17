using Shouldly;
using System;
using System.IO;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The words of the problems of an import: the place of a cell, a line for each problem, the message of the exception, and
    /// the report that a person saves.
    /// </summary>
    public class ImportIssueTextTests
    {
        private static ImportIssue Issue(int row, int column, string message = @"A problem.") =>
            new()
            {
                Code = ImportIssueCodes.NotANumber,
                Message = message,
                Sheet = @"Activities",
                Row = row,
                Column = column,
            };

        #region The address of a cell

        [Theory]
        [InlineData(1, @"A")]
        [InlineData(2, @"B")]
        [InlineData(26, @"Z")]
        [InlineData(27, @"AA")]
        [InlineData(52, @"AZ")]
        [InlineData(53, @"BA")]
        [InlineData(702, @"ZZ")]
        [InlineData(703, @"AAA")]
        [InlineData(16384, @"XFD")]    // The last column of a sheet of Excel.
        public void ColumnLetters_Given_ANumber_Then_ItIsTheLettersThatExcelGivesToTheColumn(int column, string expected)
        {
            ImportIssue.ColumnLetters(column).ShouldBe(expected);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public void ColumnLetters_Given_ANumberBelowOne_Then_Throws(int column)
        {
            Should.Throw<ArgumentOutOfRangeException>(() => ImportIssue.ColumnLetters(column));
        }

        [Fact]
        public void Cell_Given_ARowAndAColumn_Then_ItIsTheAddress()
        {
            Issue(14, 5).Cell.ShouldBe(@"E14");
        }

        [Fact]
        public void Cell_Given_NoColumn_Then_ThereIsNoAddress()
        {
            (Issue(14, 5) with { Column = null }).Cell.ShouldBeNull();
            (Issue(14, 5) with { Row = null }).Cell.ShouldBeNull();
        }

        #endregion

        #region A line for a problem

        [Fact]
        public void Describe_Given_AllThePlaces_Then_ItIsTheAddressTheTitleTheNameOfTheRowAndTheMessage()
        {
            ImportIssue issue = Issue(14, 5, @"""abc"" is not a whole number.") with { ColumnTitle = @"Duration", RowName = @"Design the wall" };

            ImportIssueText.Describe(issue).ShouldBe(@"Activities!E14 (Duration, Design the wall): ""abc"" is not a whole number.");
        }

        [Fact]
        public void Describe_Given_OnlyATitle_Then_ThereIsNoComma()
        {
            ImportIssueText.Describe(Issue(14, 5) with { ColumnTitle = @"Duration" }).ShouldBe(@"Activities!E14 (Duration): A problem.");
        }

        [Fact]
        public void Describe_Given_NoTitleAndNoName_Then_ItIsTheAddressAndTheMessage()
        {
            ImportIssueText.Describe(Issue(14, 5)).ShouldBe(@"Activities!E14: A problem.");
        }

        [Fact]
        public void Describe_Given_ARowAndNoColumn_Then_ItIsTheRowOfTheSheet()
        {
            ImportIssueText.Describe(Issue(3, 1) with { Column = null }).ShouldBe(@"Activities!3:3: A problem.");
        }

        [Fact]
        public void Describe_Given_ASheetOnly_Then_ItIsTheSheetAndTheMessage()
        {
            ImportIssueText.Describe(Issue(3, 1) with { Column = null, Row = null }).ShouldBe(@"Activities: A problem.");
        }

        [Fact]
        public void Describe_Given_NoSheet_Then_ItIsTheMessage()
        {
            ImportIssueText.Describe(new ImportIssue { Code = ImportIssueCodes.NoKnownSheet, Message = @"Nothing to read." }).ShouldBe(@"Nothing to read.");
        }

        // Excel writes a name that is more than a word in quotes, and an apostrophe in the name as two.
        [Theory]
        [InlineData(@"Resource Tracker (2)", @"'Resource Tracker (2)'!E14")]
        [InlineData(@"Holiday's", @"'Holiday''s'!E14")]
        [InlineData(@"Activity_Tracker", @"Activity_Tracker!E14")]
        public void Describe_Given_ASheetNameThatIsNotAWord_Then_ItIsInQuotes(string sheet, string expectedPlace)
        {
            ImportIssueText.Describe(Issue(14, 5) with { Sheet = sheet }).ShouldBe($@"{expectedPlace}: A problem.");
        }

        #endregion

        #region The lines and the message

        [Fact]
        public void BuildLines_Given_FewProblems_Then_ThereIsOneLineForEachAndNothingMore()
        {
            ImportIssue[] issues = [Issue(2, 1), Issue(3, 1), Issue(4, 1)];

            ImportIssueText.BuildLines(issues, 3).Split('\n').ShouldBe(issues.Select(ImportIssueText.Describe));
        }

        [Fact]
        public void BuildLines_Given_MoreProblemsThanTheLimit_Then_TheFirstAreTold_AndTheNumberOfTheOthers()
        {
            ImportIssue[] issues = [.. Enumerable.Range(2, 12).Select(x => Issue(x, 1))];

            string[] lines = ImportIssueText.BuildLines(issues, 12).Split('\n');

            lines.Length.ShouldBe(11);
            lines[9].ShouldBe(ImportIssueText.Describe(issues[9]));
            lines[10].ShouldBe(@"And 2 more.");
        }

        [Fact]
        public void BuildLines_Given_ProblemsThatWereNotKept_Then_TheyAreInTheNumberOfTheOthers()
        {
            ImportIssue[] issues = [Issue(2, 1), Issue(3, 1)];

            ImportIssueText.BuildLines(issues, 500).Split('\n').Last().ShouldBe(@"And 498 more.");
        }

        [Fact]
        public void BuildLines_Given_ALimit_Then_ThatManyAreTold()
        {
            ImportIssue[] issues = [.. Enumerable.Range(2, 30).Select(x => Issue(x, 1))];

            ImportIssueText.BuildLines(issues, 30, limit: 25).Split('\n').Length.ShouldBe(26);
            ImportIssueText.BuildLines(issues, 30, limit: int.MaxValue).Split('\n').Length.ShouldBe(30);
        }

        [Fact]
        public void BuildMessage_Given_Problems_Then_ItIsTheHeaderAndTheLines()
        {
            ImportIssue[] issues = [Issue(2, 1), Issue(3, 1)];

            ImportIssueText.BuildMessage(issues, 2).ShouldBe($"{ImportIssueText.BuildHeader(2)}\n{ImportIssueText.BuildLines(issues, 2)}");
            ImportIssueText.BuildHeader(2).ShouldBe(@"The import was cancelled and nothing was changed. Problems found in the file: 2.");
        }

        [Fact]
        public void ScenarioImportException_Given_FewerTotalThanIssues_Then_TheTotalIsAtLeastTheIssues()
        {
            var exception = new ScenarioImportException(@"A message.", [Issue(2, 1), Issue(3, 1)], 0);

            exception.TotalCount.ShouldBe(2);
            new ScenarioImportException().Issues.ShouldBeEmpty();
        }

        #endregion

        #region The report

        private static string Report(params ImportIssue[] issues)
        {
            using var writer = new StringWriter();
            ImportIssueText.WriteReport(writer, issues);
            return writer.ToString();
        }

        [Fact]
        public void WriteReport_Given_Problems_Then_ThereIsARowOfTitlesAndARowForEachProblem()
        {
            ImportIssue issue = Issue(14, 5, @"""abc"" is not a whole number.") with
            {
                Code = ImportIssueCodes.NotAWholeNumber,
                ColumnTitle = @"Duration",
                RowName = @"Design the wall",
                Value = @"abc",
            };

            Report(issue, Issue(3, 1) with { Column = null, Row = null, Sheet = null }).ShouldBe(
                "Sheet,Cell,Row,Column,Column title,Row name,Value,Code,Message\n" +
                "Activities,E14,14,5,Duration,Design the wall,abc,notAWholeNumber,\"\"\"abc\"\" is not a whole number.\"\n" +
                ",,,,,,,notANumber,A problem.\n");
        }

        [Fact]
        public void WriteReport_Given_NoProblems_Then_ThereAreOnlyTheTitles()
        {
            Report().ShouldBe("Sheet,Cell,Row,Column,Column title,Row name,Value,Code,Message\n");
        }

        // A field with a comma, a quote or a line end is in quotes, as RFC 4180 says.
        [Fact]
        public void WriteReport_Given_FieldsWithCommasQuotesAndLineEnds_Then_TheyAreQuoted()
        {
            string report = Report(Issue(2, 1) with { RowName = "one, two", Value = "line 1\nline 2", ColumnTitle = "say \"hello\"" });

            report.ShouldContain("\"say \"\"hello\"\"\",\"one, two\",\"line 1\nline 2\"");
        }

        // The report is opened in a spreadsheet, and a field that starts as a formula starts is a formula there. The text came from
        // the cells of a file that anyone could have written.
        [Theory]
        [InlineData(@"=1+1")]
        [InlineData(@"+1")]
        [InlineData(@"-1")]
        [InlineData(@"@SUM(A1)")]
        public void WriteReport_Given_AValueThatStartsAsAFormulaDoes_Then_ItStartsWithAnApostrophe(string value)
        {
            Report(Issue(2, 1) with { Value = value }).ShouldContain($",'{value},");
        }

        #endregion
    }
}
