using Shouldly;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using static Zametek.ViewModel.ProjectPlan.Tests.XlsxCellSpec;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// What a workbook means to the importer, cell by cell. A cell is read as what it is: a number is a number in every culture,
    /// a date is a date, a truth value is a truth value, and a formula is its result. A cell of text can hold a value too, and
    /// is read in the culture of the person who imports, as strictly as can be. These tests make workbooks of the kinds that
    /// Excel holds, by their XML alone, and say what the importer makes of each: the value, or the problem that cancels the
    /// import (the problems and where they are: see <see cref="XlsxScenarioImportIssueTests"/>).
    /// </summary>
    public class XlsxScenarioImportContractTests
    {
        private static readonly TimeProvider s_Clock =
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);

        private static readonly string s_Activities = Resource.ProjectPlan.Reporting.Reporting_WorksheetActivities;
        private static readonly string s_Resources = Resource.ProjectPlan.Reporting.Reporting_WorksheetResources;
        private static readonly string s_General = Resource.ProjectPlan.Reporting.Reporting_WorksheetGeneral;
        private static readonly string s_ActivityTracker = Resource.ProjectPlan.Reporting.Reporting_WorksheetActivityTracker;

        #region Helpers

        // The kinds of cell that a theory names, as the strings that a theory can hold.
        private static XlsxCellSpec? Cell(string kind, string value) =>
            kind switch
            {
                @"missing" => null,
                @"blank" => StyledBlank(),
                @"shared" => TextOf(value),
                @"inline" => InlineText(value),
                @"number" => NumberOf(double.Parse(value, CultureInfo.InvariantCulture)),
                @"bool" => BooleanOf(bool.Parse(value)),
                @"date" => DateOf(DateTime.Parse(value, CultureInfo.InvariantCulture)),
                @"error" => ErrorOf(value),
                @"formula-number" => FormulaNumber(value.Split('=')[0], double.Parse(value.Split('=')[1], CultureInfo.InvariantCulture)),
                @"formula-text" => FormulaText(value.Split('=')[0], value.Split('=')[1]),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            };

        private static T WithCulture<T>(string culture, Func<T> action)
        {
            CultureInfo previous = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = new CultureInfo(culture);
                return action();
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        private static ProjectScenarioImportModel Import(byte[] workbook, string culture = @"en-GB") =>
            WithCulture(culture, () =>
            {
                using var stream = new MemoryStream(workbook);
                return new XlsxScenarioFileImporter(new DateTimeCalculator(s_Clock)).ImportProjectScenarioXlsxFile(stream);
            });

        private static ScenarioImportException ImportFailure(byte[] workbook, string culture = @"en-GB") =>
            Should.Throw<ScenarioImportException>(() => Import(workbook, culture));

        // A workbook with an activity that has an Id of 1 and one more column, whose cell is the one under test.
        private static byte[] ActivityWorkbook(string column, XlsxCellSpec? cell) =>
            new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s
                    .Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(column)))
                    .Row(2, [("A", NumberOf(1.0)), .. cell is null ? [] : new[] { ("B", cell) }]))
                .Build();

        private static ActivityModel? ImportActivity(string column, XlsxCellSpec? cell, string culture = @"en-GB") =>
            Import(ActivityWorkbook(column, cell), culture).DependentActivities.Select(x => x.Activity).SingleOrDefault();

        // The one problem of the cell under test.
        private static ImportIssue ActivityIssue(string column, XlsxCellSpec? cell, string culture = @"en-GB")
        {
            ScenarioImportException failure = ImportFailure(ActivityWorkbook(column, cell), culture);

            failure.TotalCount.ShouldBe(1);
            return failure.Issues.Single();
        }

        private static byte[] ResourceWorkbook(string column, XlsxCellSpec? cell) =>
            new XlsxPackageBuilder()
                .Sheet(s_Resources, s => s
                    .Row(1, ("A", TitleOf(nameof(ResourceModel.Id))), ("B", TitleOf(column)))
                    .Row(2, [("A", NumberOf(1.0)), .. cell is null ? [] : new[] { ("B", cell) }]))
                .Build();

        private static ResourceModel? ImportResource(string column, XlsxCellSpec? cell, string culture = @"en-GB") =>
            Import(ResourceWorkbook(column, cell), culture).ResourceSettings.Resources.SingleOrDefault();

        #endregion

        #region Whole numbers

        [Theory]
        [InlineData(@"number", @"5", 5)]
        [InlineData(@"number", @"-3", -3)]
        [InlineData(@"number", @"2147483647", int.MaxValue)]
        [InlineData(@"shared", @"5", 5)]
        [InlineData(@"shared", @" 5 ", 5)]            // The spaces around a number are allowed.
        [InlineData(@"inline", @"5", 5)]
        [InlineData(@"formula-number", @"2+3=5", 5)]  // A formula is its result, the one that the file keeps.
        [InlineData(@"blank", @"", 0)]                // A cell with no value gives the default of the column.
        [InlineData(@"missing", @"", 0)]
        public void Import_Given_ACellInAColumnOfWholeNumbers_Then_ItIsTheWholeNumberThatItHolds(string kind, string value, int expected)
        {
            ActivityModel? activity = ImportActivity(nameof(ActivityModel.Duration), Cell(kind, value));

            activity.ShouldNotBeNull().Duration.ShouldBe(expected);
        }

        // Anything else is a problem, and nothing is imported: not a number with a part that is not whole, not one that is too
        // large, not text that is not a number, not another kind of cell, and not an error of Excel.
        [Theory]
        [InlineData(@"number", @"5.5", @"5.5", ImportIssueCodes.NotAWholeNumber)]
        [InlineData(@"number", @"10000000000", @"10000000000", ImportIssueCodes.NotAWholeNumber)]
        [InlineData(@"shared", @"5.0", @"5.0", ImportIssueCodes.NotAWholeNumber)]
        [InlineData(@"shared", @"five", @"five", ImportIssueCodes.NotAWholeNumber)]
        [InlineData(@"bool", @"true", @"TRUE", ImportIssueCodes.NotAWholeNumber)]
        [InlineData(@"date", @"2026-10-17", @"2026-10-17", ImportIssueCodes.NotAWholeNumber)]
        [InlineData(@"error", @"#N/A", @"#N/A", ImportIssueCodes.ExcelError)]
        public void Import_Given_ACellInAColumnOfWholeNumbersThatHoldsSomethingElse_Then_TheImportIsCancelledAtThatCell(string kind, string value, string shown, string code)
        {
            ImportIssue issue = ActivityIssue(nameof(ActivityModel.Duration), Cell(kind, value));

            issue.Code.ShouldBe(code);
            issue.Cell.ShouldBe(@"B2");
            issue.Value.ShouldBe(shown);
        }

        // The estimates that a workbook does not give are null, and an estimate of 0 is an estimate.
        [Theory]
        [InlineData(@"blank", @"", null)]
        [InlineData(@"missing", @"", null)]
        [InlineData(@"number", @"0", 0)]
        [InlineData(@"number", @"4", 4)]
        public void Import_Given_ACellInAColumnOfEstimates_Then_ItIsNullUnlessItHoldsAWholeNumber(string kind, string value, int? expected)
        {
            ImportActivity(nameof(ActivityModel.OptimisticDuration), Cell(kind, value)).ShouldNotBeNull().OptimisticDuration.ShouldBe(expected);
        }

        [Fact]
        public void Import_Given_AnEstimateThatIsNotANumber_Then_TheImportIsCancelledAtThatCell()
        {
            ImportIssue issue = ActivityIssue(nameof(ActivityModel.OptimisticDuration), TextOf(@"nine"));

            (issue.Code, issue.Cell, issue.Value).ShouldBe((ImportIssueCodes.NotAWholeNumber, @"B2", @"nine"));
        }

        // The Id is the one column that has to hold a number for the row to name an activity.
        [Theory]
        [InlineData(@"number", @"7", 7)]
        [InlineData(@"shared", @"7", 7)]
        public void Import_Given_ACellInTheColumnOfIds_Then_ItIsTheIdOfTheActivity(string kind, string value, int expected)
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s
                    .Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(nameof(ActivityModel.Name))))
                    .Row(2, ("A", Cell(kind, value)!), ("B", TextOf(@"named"))))
                .Build();

            ActivityModel activity = Import(workbook).DependentActivities.Single().Activity;

            activity.Id.ShouldBe(expected);
            activity.Name.ShouldBe(@"named");
        }

        [Theory]
        [InlineData(@"shared", @"seven", ImportIssueCodes.NotAWholeNumber)]
        [InlineData(@"blank", @"", ImportIssueCodes.MissingValue)]
        [InlineData(@"missing", @"", ImportIssueCodes.MissingValue)]
        public void Import_Given_AnIdThatIsNotANumberOrIsNotThere_Then_TheImportIsCancelledAtThatCell(string kind, string value, string code)
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s
                    .Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(nameof(ActivityModel.Name))))
                    .Row(2, [.. Cell(kind, value) is XlsxCellSpec cell ? new[] { ("A", cell) } : [], ("B", TextOf(@"named"))]))
                .Build();

            ImportIssue issue = ImportFailure(workbook).Issues.Single();

            (issue.Code, issue.Cell, issue.RowName).ShouldBe((code, @"A2", @"named"));
        }

        #endregion

        #region Truth values

        [Theory]
        [InlineData(@"bool", @"true", true)]
        [InlineData(@"bool", @"false", false)]
        [InlineData(@"shared", @"true", true)]
        [InlineData(@"shared", @"TRUE", true)]
        [InlineData(@"shared", @" True ", true)]
        [InlineData(@"shared", @"false", false)]
        [InlineData(@"formula-text", @"T(1)=true", true)]  // A formula is its result.
        [InlineData(@"blank", @"", false)]                // A cell with no value gives the default of the column.
        [InlineData(@"missing", @"", false)]
        public void Import_Given_ACellInAColumnOfTruthValues_Then_ItIsTheTruthValueThatItHolds(string kind, string value, bool expected)
        {
            ImportActivity(nameof(ActivityModel.HasNoCost), Cell(kind, value)).ShouldNotBeNull().HasNoCost.ShouldBe(expected);
        }

        // A truth value is true or false, and not "yes", and not a number.
        [Theory]
        [InlineData(@"shared", @"yes", @"yes")]
        [InlineData(@"number", @"1", @"1")]
        [InlineData(@"date", @"2026-10-17", @"2026-10-17")]
        public void Import_Given_ACellInAColumnOfTruthValuesThatHoldsSomethingElse_Then_TheImportIsCancelledAtThatCell(string kind, string value, string shown)
        {
            ImportIssue issue = ActivityIssue(nameof(ActivityModel.HasNoCost), Cell(kind, value));

            (issue.Code, issue.Cell, issue.Value).ShouldBe((ImportIssueCodes.NotTrueOrFalse, @"B2", shown));
        }

        #endregion

        #region Text

        [Theory]
        [InlineData(@"shared", @"a name", @"a name")]
        [InlineData(@"inline", @"a name", @"a name")]
        [InlineData(@"shared", @"  spaced  ", @"  spaced  ")]
        [InlineData(@"number", @"5", @"5")]
        [InlineData(@"number", @"1.5", @"1.5")]
        [InlineData(@"bool", @"true", @"TRUE")]
        [InlineData(@"bool", @"false", @"FALSE")]
        [InlineData(@"blank", @"", @"")]
        [InlineData(@"missing", @"", @"")]
        [InlineData(@"formula-text", @"""a""&""b""=ab", @"ab")]   // A formula is its result.
        [InlineData(@"formula-number", @"1+1=2", @"2")]
        public void Import_Given_ACellInAColumnOfText_Then_ItIsTheTextThatItGives(string kind, string value, string expected)
        {
            ImportActivity(nameof(ActivityModel.Name), Cell(kind, value)).ShouldNotBeNull().Name.ShouldBe(expected);
        }

        [Fact]
        public void Import_Given_AnErrorOfExcelInAColumnOfText_Then_TheImportIsCancelledAtThatCell()
        {
            ImportIssue issue = ActivityIssue(nameof(ActivityModel.Name), ErrorOf(@"#N/A"));

            (issue.Code, issue.Cell, issue.Value).ShouldBe((ImportIssueCodes.ExcelError, @"B2", @"#N/A"));
        }

        // A date in a column of text is the day, in the form that does not depend on the culture of the person who imports.
        [Theory]
        [InlineData(@"en-GB")]
        [InlineData(@"en-US")]
        [InlineData(@"de-DE")]
        [InlineData(@"fr-FR")]
        [InlineData(@"tr-TR")]
        [InlineData(@"ja-JP")]
        public void Import_Given_ADateInAColumnOfText_Then_ItIsTheDayInTheSameFormInEveryCulture(string culture)
        {
            ImportActivity(nameof(ActivityModel.Name), DateOf(new DateTime(2026, 10, 17)), culture).ShouldNotBeNull().Name.ShouldBe(@"2026-10-17");
        }

        #endregion

        #region Dates

        // A date cell is the date that it holds, in every culture. A cell of text is the date that it says, in the culture of the
        // person who imports.
        [Theory]
        [InlineData(@"en-GB", @"date", @"2026-10-17", @"2026-10-17")]
        [InlineData(@"en-US", @"date", @"2026-10-17", @"2026-10-17")]
        [InlineData(@"de-DE", @"date", @"2026-10-17", @"2026-10-17")]
        [InlineData(@"fr-FR", @"date", @"2026-10-17", @"2026-10-17")]
        [InlineData(@"tr-TR", @"date", @"2026-10-17", @"2026-10-17")]
        [InlineData(@"ja-JP", @"date", @"2026-10-17", @"2026-10-17")]
        [InlineData(@"en-GB", @"shared", @"17/10/2026", @"2026-10-17")]
        [InlineData(@"en-US", @"shared", @"10/17/2026", @"2026-10-17")]
        [InlineData(@"en-GB", @"shared", @"2026-10-17", @"2026-10-17")]
        [InlineData(@"en-GB", @"blank", @"", null)]
        [InlineData(@"en-GB", @"missing", @"", null)]
        public void Import_Given_ACellInAColumnOfDates_Then_ItIsTheDateThatItHolds(string culture, string kind, string value, string? expected)
        {
            ActivityModel activity = ImportActivity(nameof(ActivityModel.MinimumEarliestStartDateTime), Cell(kind, value), culture).ShouldNotBeNull();

            // The offset is the one of the machine, and so only the day is compared.
            activity.MinimumEarliestStartDateTime?.DateTime.ToString(@"yyyy-MM-dd", CultureInfo.InvariantCulture).ShouldBe(expected);
            (activity.MinimumEarliestStartDateTime is null).ShouldBe(expected is null);
        }

        // A number is not a date unless the cell has the format of one.
        [Theory]
        [InlineData(@"shared", @"not a date", @"not a date")]
        [InlineData(@"number", @"46312", @"46312")]
        [InlineData(@"bool", @"true", @"TRUE")]
        public void Import_Given_ACellInAColumnOfDatesThatHoldsSomethingElse_Then_TheImportIsCancelledAtThatCell(string kind, string value, string shown)
        {
            ImportIssue issue = ActivityIssue(nameof(ActivityModel.MinimumEarliestStartDateTime), Cell(kind, value));

            (issue.Code, issue.Cell, issue.Value).ShouldBe((ImportIssueCodes.NotADate, @"B2", shown));
        }

        [Fact]
        public void Import_Given_ADateWithATimeOfDay_Then_OnlyTheDayIsKept()
        {
            ActivityModel activity = ImportActivity(nameof(ActivityModel.MinimumEarliestStartDateTime), DateOf(new DateTime(2026, 10, 17, 13, 45, 30))).ShouldNotBeNull();

            activity.MinimumEarliestStartDateTime!.Value.DateTime.ShouldBe(new DateTime(2026, 10, 17));
        }

        #endregion

        #region Decimal numbers

        [Theory]
        [InlineData(@"en-GB", @"number", @"1.5", 1.5)]
        [InlineData(@"de-DE", @"number", @"1.5", 1.5)]      // A number cell is the same number in every culture.
        [InlineData(@"fr-FR", @"number", @"1234.5", 1234.5)]
        [InlineData(@"tr-TR", @"number", @"0.25", 0.25)]
        [InlineData(@"ja-JP", @"number", @"2.75", 2.75)]
        [InlineData(@"en-GB", @"shared", @"1.5", 1.5)]      // A number typed as text is written as the person writes numbers.
        [InlineData(@"de-DE", @"shared", @"1,5", 1.5)]
        [InlineData(@"en-GB", @"formula-number", @"1+0.5=1.5", 1.5)]
        [InlineData(@"en-GB", @"blank", @"", 0.0)]          // The default of the column is 0.
        [InlineData(@"en-GB", @"missing", @"", 0.0)]
        public void Import_Given_ACellInAColumnOfDecimalNumbers_Then_ItIsTheNumberThatItHolds(string culture, string kind, string value, double expected)
        {
            ImportResource(nameof(ResourceModel.UnitCost), Cell(kind, value), culture).ShouldNotBeNull().UnitCost.ShouldBe(expected);
        }

        // A number typed as text is read in the culture of the person who imports, and as strictly as can be: "1.5" is not
        // fifteen where a point separates the thousands, and "1,5" is not 1.5 where a comma does.
        [Theory]
        [InlineData(@"de-DE", @"1.5")]
        [InlineData(@"en-GB", @"1,5")]
        [InlineData(@"en-GB", @"1,000")]
        [InlineData(@"en-GB", @"nothing")]
        [InlineData(@"en-GB", @"NaN")]
        public void Import_Given_ATextThatIsNotANumberInTheCultureOfTheImport_Then_TheImportIsCancelledAtThatCell(string culture, string text)
        {
            ScenarioImportException failure = ImportFailure(ResourceWorkbook(nameof(ResourceModel.UnitCost), TextOf(text)), culture);

            ImportIssue issue = failure.Issues.Single();
            (issue.Code, issue.Sheet, issue.Cell, issue.Value).ShouldBe((ImportIssueCodes.NotANumber, s_Resources, @"B2", text));
        }

        #endregion

        #region Choices and colors

        [Theory]
        [InlineData(@"Weekends", NonWorkingDayMode.Weekends)]
        [InlineData(@"weekends", NonWorkingDayMode.Weekends)]
        [InlineData(@" CustomCalendar ", NonWorkingDayMode.CustomCalendar)]
        [InlineData(@"None", NonWorkingDayMode.None)]
        public void Import_Given_TheNameOfAChoice_Then_ItIsTheChoice(string text, NonWorkingDayMode expected)
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetDisplaySettings, s => s.Table([nameof(ProjectScenarioModel.DisplaySettings.NonWorkingDayMode)], [text]))
                .Build();

            Import(workbook).DisplaySettings.NonWorkingDayMode.ShouldBe(expected);
        }

        [Fact]
        public void Import_Given_AWordThatIsNotAChoice_Then_TheImportIsCancelledAndTheChoicesAreSaid()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetDisplaySettings, s => s.Table([nameof(ProjectScenarioModel.DisplaySettings.NonWorkingDayMode)], [@"Custom Calendar"]))
                .Build();

            ImportIssue issue = ImportFailure(workbook).Issues.Single();

            (issue.Code, issue.Cell, issue.Value).ShouldBe((ImportIssueCodes.NotAChoice, @"A2", @"Custom Calendar"));
            issue.Message.ShouldContain(@"None, Weekends, CustomCalendar");
        }

        [Theory]
        [InlineData(@"#FF0000", 255, 0, 0, 255)]
        [InlineData(@"#00FF0080", 0, 255, 0, 128)]
        public void Import_Given_AColor_Then_ItIsTheColor(string text, int r, int g, int b, int a)
        {
            ColorFormatModel color = ImportResource(nameof(ResourceModel.ColorFormat), TextOf(text)).ShouldNotBeNull().ColorFormat;

            (color.R, color.G, color.B, color.A).ShouldBe(((byte)r, (byte)g, (byte)b, (byte)a));
        }

        [Theory]
        [InlineData(@"red")]
        [InlineData(@"#FF00")]
        [InlineData(@"FF0000")]
        public void Import_Given_ATextThatIsNotAColor_Then_TheImportIsCancelledAtThatCell(string text)
        {
            ImportIssue issue = ImportFailure(ResourceWorkbook(nameof(ResourceModel.ColorFormat), TextOf(text))).Issues.Single();

            (issue.Code, issue.Cell, issue.Value).ShouldBe((ImportIssueCodes.NotAColor, @"B2", text));
        }

        #endregion

        #region Lists of numbers

        [Theory]
        [InlineData(@"shared", @"1,2,3", new[] { 1, 2, 3 })]
        [InlineData(@"shared", @" 1 , 2 ", new[] { 1, 2 })]
        [InlineData(@"shared", @"1,,2,", new[] { 1, 2 })]    // A place with nothing in it is left out.
        [InlineData(@"number", @"7", new[] { 7 })]           // A single number is a list of one.
        [InlineData(@"blank", @"", new int[0])]
        [InlineData(@"missing", @"", new int[0])]
        public void Import_Given_ACellInAColumnOfDependencies_Then_ItIsTheListOfIds(string kind, string value, int[] expected)
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s
                    .Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(nameof(DependentActivityModel.Dependencies))))
                    .Row(2, [("A", NumberOf(1.0)), .. Cell(kind, value) is XlsxCellSpec cell ? new[] { ("B", cell) } : []]))
                .Build();

            Import(workbook).DependentActivities.Single().Dependencies.ShouldBe(expected);
        }

        // Each place in the list that is not a number is a problem of its own, at the cell that holds the list.
        [Fact]
        public void Import_Given_ADependencyThatIsNotANumber_Then_TheImportIsCancelledAtThatCell()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s
                    .Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(nameof(DependentActivityModel.Dependencies))))
                    .Row(2, ("A", NumberOf(1.0)), ("B", TextOf(@"2,x,3,y"))))
                .Build();

            ScenarioImportException failure = ImportFailure(workbook);

            failure.Issues.Select(x => (x.Code, x.Cell, x.Value)).ShouldBe(
            [
                (ImportIssueCodes.NotAWholeNumber, @"B2", @"x"),
                (ImportIssueCodes.NotAWholeNumber, @"B2", @"y"),
            ]);
        }

        #endregion

        #region The shape of a sheet

        [Fact]
        public void Import_Given_ASheetThatTheImporterDoesNotKnow_Then_ItIsLeftAlone()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(@"Another sheet", s => s.Table([nameof(ActivityModel.Id)], [5]))
                .Sheet(s_Activities, s => s.Table([nameof(ActivityModel.Id), nameof(ActivityModel.Name)], [1, @"one"]))
                .Build();

            Import(workbook).DependentActivities.Select(x => x.Activity.Id).ShouldBe([1]);
        }

        [Fact]
        public void Import_Given_ASheetNamedInAnotherCase_Then_ItIsFound()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities.ToUpperInvariant(), s => s.Table([nameof(ActivityModel.Id), nameof(ActivityModel.Name)], [1, @"one"]))
                .Build();

            Import(workbook).DependentActivities.Single().Activity.Name.ShouldBe(@"one");
        }

        [Fact]
        public void Import_Given_TitlesInAnotherCase_Then_TheColumnsAreFound()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s.Table([@"id", @"NAME", @"duration"], [3, @"three", 8]))
                .Build();

            ActivityModel activity = Import(workbook).DependentActivities.Single().Activity;

            (activity.Id, activity.Name, activity.Duration).ShouldBe((3, @"three", 8));
        }

        [Fact]
        public void Import_Given_ATitleWithASpaceAroundIt_Then_ItIsNotTheColumn()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s.Table([nameof(ActivityModel.Id), @" Name "], [3, @"three"]))
                .Build();

            // The name was not read, and the activity has the one that it has when nothing gives it.
            Import(workbook).DependentActivities.Single().Activity.Name.ShouldBeEmpty();
        }

        // A workbook that has more than the importer reads is a workbook that can be imported: a column that the importer does not
        // know is left alone, whatever it holds, and so is a title that two columns have.
        [Fact]
        public void Import_Given_ATitleThatIsNotOneOfTheImporters_Then_ItsColumnIsLeftAlone()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s.Table([nameof(ActivityModel.Id), @"My own notes", nameof(ActivityModel.Name), @"My own notes"], [3, @"for me", @"three", 12.5]))
                .Build();

            ActivityModel activity = Import(workbook).DependentActivities.Single().Activity;

            (activity.Id, activity.Name).ShouldBe((3, @"three"));
        }

        // The titles end at the first one that is blank, and a column after it is not read, whatever it holds.
        [Fact]
        public void Import_Given_ABlankTitle_Then_TheColumnsAfterItAreNotRead()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s
                    .Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(nameof(ActivityModel.Name))), ("D", TitleOf(nameof(ActivityModel.Duration))))
                    .Row(2, ("A", NumberOf(1.0)), ("B", TextOf(@"one")), ("D", NumberOf(9.0))))
                .Build();

            ActivityModel activity = Import(workbook).DependentActivities.Single().Activity;

            (activity.Name, activity.Duration).ShouldBe((@"one", 0));
        }

        [Fact]
        public void Import_Given_AValueUnderNoTitle_Then_ItIsLeftAlone()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s
                    .Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(nameof(ActivityModel.Name))))
                    .Row(2, ("A", NumberOf(1.0)), ("B", TextOf(@"one")), ("C", TextOf(@"stray")), ("F", NumberOf(99.0))))
                .Build();

            Import(workbook).DependentActivities.Single().Activity.Name.ShouldBe(@"one");
        }

        [Fact]
        public void Import_Given_RowsThatHoldNothing_Then_NoActivityIsMadeOfThem()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s
                    .Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(nameof(ActivityModel.Name))))
                    .Row(2, ("A", NumberOf(1.0)), ("B", TextOf(@"one")))
                    .Row(3, ("A", StyledBlank()), ("B", StyledBlank()))     // A row that was formatted and left empty.
                    .Row(5, ("A", NumberOf(2.0)), ("B", TextOf(@"two"))))    // And row 4 has no cell at all.
                .Build();

            Import(workbook).DependentActivities.Select(x => x.Activity.Id).Order().ShouldBe([1, 2]);
        }

        // A row that holds a value only outside the columns that have a title is a row that holds nothing.
        [Fact]
        public void Import_Given_ARowThatHoldsAValueOnlyUnderNoTitle_Then_NoActivityIsMadeOfIt()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s
                    .Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(nameof(ActivityModel.Name))))
                    .Row(2, ("A", NumberOf(1.0)), ("B", TextOf(@"one")))
                    .Row(3, ("A", StyledBlank()), ("F", TextOf(@"a note of a person"))))
                .Build();

            Import(workbook).DependentActivities.Select(x => x.Activity.Id).ShouldBe([1]);
        }

        [Fact]
        public void Import_Given_ACellMissingInTheMiddleOfARow_Then_ThatValueIsBlankAndTheOthersStayInTheirColumns()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s
                    .Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(nameof(ActivityModel.Name))), ("C", TitleOf(nameof(ActivityModel.Duration))))
                    .Row(2, ("A", NumberOf(1.0)), ("C", NumberOf(7.0))))
                .Build();

            ActivityModel activity = Import(workbook).DependentActivities.Single().Activity;

            (activity.Name, activity.Duration).ShouldBe((string.Empty, 7));
        }

        // Excel leaves out a cell that is empty, so a row can have no cell in its first column. Every value is still in its own
        // column, the one under its title.
        [Fact]
        public void Import_Given_ARowThatHasNoCellInItsFirstColumn_Then_EveryValueStaysInItsOwnColumn()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s
                    .Row(1, ("A", TitleOf(nameof(ActivityModel.Name))), ("B", TitleOf(nameof(ActivityModel.Id))))
                    .Row(2, ("B", NumberOf(5.0))))
                .Build();

            ActivityModel activity = Import(workbook).DependentActivities.Single().Activity;

            (activity.Id, activity.Name).ShouldBe((5, string.Empty));
        }

        #endregion

        #region The sheets of the plan

        [Fact]
        public void Import_Given_ADateCellOnTheGeneralSheet_Then_ItIsWhenThePlanStarts()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_General, s => s.Table([nameof(ProjectScenarioImportModel.ProjectStart), nameof(ProjectScenarioImportModel.Today)], [new DateTime(2026, 3, 2), new DateTime(2026, 3, 9)]))
                .Build();

            ProjectScenarioImportModel import = Import(workbook);

            import.ProjectStart.DateTime.ShouldBe(new DateTime(2026, 3, 2));
            import.Today.DateTime.ShouldBe(new DateTime(2026, 3, 9));
        }

        [Fact]
        public void Import_Given_ABlankDateOnTheGeneralSheet_Then_ThatDayIsTheClocks()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_General, s => s.Row(1, ("A", TitleOf(nameof(ProjectScenarioImportModel.ProjectStart))), ("B", TitleOf(nameof(ProjectScenarioImportModel.Today)))).Row(2, ("A", StyledBlank()), ("B", DateOf(new DateTime(2026, 3, 9)))))
                .Build();

            ProjectScenarioImportModel import = Import(workbook);

            import.ProjectStart.DateTime.ShouldBe(new DateTime(2026, 10, 17));
            import.Today.DateTime.ShouldBe(new DateTime(2026, 3, 9));
        }

        // A General sheet whose first date was cleared in Excel (and so has no cell for it) leaves the start to the clock and takes
        // the second date as today.
        [Fact]
        public void Import_Given_AGeneralSheetWhoseFirstDateWasCleared_Then_TheSecondDateIsTodayAndTheStartIsTheClocks()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_General, s => s.Row(1, ("A", TitleOf(nameof(ProjectScenarioImportModel.ProjectStart))), ("B", TitleOf(nameof(ProjectScenarioImportModel.Today)))).Row(2, ("B", DateOf(new DateTime(2026, 3, 9)))))
                .Build();

            ProjectScenarioImportModel import = Import(workbook);

            import.ProjectStart.DateTime.ShouldBe(new DateTime(2026, 10, 17));
            import.Today.DateTime.ShouldBe(new DateTime(2026, 3, 9));
        }

        // Trackers are read by the title of the Id, and the cell in the Nth column after the Id is the percentage at time N - 1.
        [Fact]
        public void Import_Given_ATrackerSheet_Then_EachPercentageAboveZeroIsATrackerAtTheTimeOfItsColumn()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s.Table([nameof(ActivityModel.Id), nameof(ActivityModel.Name)], [1, @"one"], [2, @"two"]))
                .Sheet(s_ActivityTracker, s => s
                    .Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(@"0")), ("C", TitleOf(@"1")), ("D", TitleOf(@"2")), ("E", TitleOf(@"3")), ("F", TitleOf(@"4")))
                    .Row(2, ("A", NumberOf(1.0)), ("B", NumberOf(25.0)), ("D", NumberOf(50.0)), ("E", TextOf(@"75")))
                    .Row(3, ("A", NumberOf(2.0)), ("C", NumberOf(100.0)), ("D", NumberOf(0.0))))
                .Build();

            var activities = Import(workbook).DependentActivities.Select(x => x.Activity).ToDictionary(x => x.Id);

            // A percentage of 0 is no tracker.
            activities[1].Trackers.Select(x => (x.Time, x.PercentageComplete)).ShouldBe([(0, 25), (2, 50), (3, 75)]);
            activities[2].Trackers.Select(x => (x.Time, x.PercentageComplete)).ShouldBe([(1, 100)]);
        }

        // The Id is found by its title, and the times are the columns after it, wherever it is.
        [Fact]
        public void Import_Given_ATrackerSheetWhoseIdIsNotInTheFirstColumn_Then_TheTimesAreCountedFromTheId()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s.Table([nameof(ActivityModel.Id), nameof(ActivityModel.Name)], [1, @"one"]))
                .Sheet(s_ActivityTracker, s => s
                    .Row(1, ("A", TitleOf(@"A note")), ("B", TitleOf(nameof(ActivityModel.Id))), ("C", TitleOf(@"0")), ("D", TitleOf(@"1")))
                    .Row(2, ("A", TextOf(@"by someone")), ("B", NumberOf(1.0)), ("D", NumberOf(40.0))))
                .Build();

            Import(workbook).DependentActivities.Single().Activity.Trackers.Select(x => (x.Time, x.PercentageComplete)).ShouldBe([(1, 40)]);
        }

        #endregion
    }
}
