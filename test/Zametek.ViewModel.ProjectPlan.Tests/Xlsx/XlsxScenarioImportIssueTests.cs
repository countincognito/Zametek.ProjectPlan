using Shouldly;
using System;
using System.IO;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using static Zametek.ViewModel.ProjectPlan.Tests.XlsxCellSpec;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// What the import does with a workbook that has problems: it reads the whole workbook, collects each problem with its place
    /// (the sheet, the row, the column, the cell), and then cancels, with all of them in the exception. A workbook that has no
    /// problem is imported whole. The cells and what each kind of cell means are in <see cref="XlsxScenarioImportContractTests"/>.
    /// </summary>
    public class XlsxScenarioImportIssueTests
    {
        private static readonly TimeProvider s_Clock =
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);

        private static readonly string s_Activities = Resource.ProjectPlan.Reporting.Reporting_WorksheetActivities;
        private static readonly string s_Resources = Resource.ProjectPlan.Reporting.Reporting_WorksheetResources;
        private static readonly string s_Holidays = Resource.ProjectPlan.Reporting.Reporting_WorksheetHolidays;
        private static readonly string s_General = Resource.ProjectPlan.Reporting.Reporting_WorksheetGeneral;
        private static readonly string s_ActivityTracker = Resource.ProjectPlan.Reporting.Reporting_WorksheetActivityTracker;
        private static readonly string s_ResourceTracker = $@"{Resource.ProjectPlan.Reporting.Reporting_WorksheetResourceTracker} (1)";

        #region Helpers

        private static ProjectScenarioImportModel Import(byte[] workbook)
        {
            using var stream = new MemoryStream(workbook);
            return new XlsxScenarioFileImporter(new DateTimeCalculator(s_Clock)).ImportProjectScenarioXlsxFile(stream);
        }

        private static ScenarioImportException ImportFailure(byte[] workbook) =>
            Should.Throw<ScenarioImportException>(() => Import(workbook));

        // The place and the kind of each problem, in the order in which the exception holds them.
        private static (string Code, string? Sheet, string? Cell)[] Places(ScenarioImportException failure) =>
            [.. failure.Issues.Select(x => (x.Code, x.Sheet, x.Cell))];

        private static byte[] Activities(Action<XlsxSheetSpec> fill) =>
            new XlsxPackageBuilder().Sheet(s_Activities, fill).Build();

        #endregion

        #region The workbook as a whole

        [Fact]
        public void Import_Given_AWorkbookWithNoSheet_Then_TheImportIsCancelledAsThereIsNothingToImport()
        {
            ScenarioImportException failure = ImportFailure(new XlsxPackageBuilder().Build());

            failure.TotalCount.ShouldBe(1);
            ImportIssue issue = failure.Issues.Single();
            (issue.Code, issue.Sheet, issue.Cell).ShouldBe((ImportIssueCodes.NoKnownSheet, null, null));
        }

        [Fact]
        public void Import_Given_AWorkbookWithOnlySheetsThatTheImporterDoesNotKnow_Then_TheImportIsCancelled()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(@"Another sheet", s => s.Table([@"Id"], [5]))
                .Build();

            Places(ImportFailure(workbook)).ShouldBe([(ImportIssueCodes.NoKnownSheet, null, null)]);
        }

        // A workbook with a sheet that the importer knows is a workbook of the application, even if the sheet holds nothing.
        [Fact]
        public void Import_Given_ASheetOfTitlesAndNothingElse_Then_ThereIsNothingToImportAndNoProblem()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s.Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(nameof(ActivityModel.Name)))))
                .Build();

            Import(workbook).DependentActivities.ShouldBeEmpty();
        }

        [Fact]
        public void Import_Given_BytesThatAreNotAWorkbook_Then_TheImportIsCancelledWithOneProblemAboutTheFile()
        {
            ScenarioImportException failure = ImportFailure(Enumerable.Range(0, 2048).Select(x => (byte)(x * 7)).ToArray());

            failure.TotalCount.ShouldBe(1);
            ImportIssue issue = failure.Issues.Single();
            (issue.Code, issue.Sheet, issue.Row).ShouldBe((ImportIssueCodes.UnreadableWorkbook, null, null));
            issue.Message.ShouldStartWith(@"The file cannot be read as a workbook.");
        }

        [Fact]
        public void Import_Given_AnEmptyFile_Then_TheImportIsCancelledWithOneProblemAboutTheFile()
        {
            Places(ImportFailure([])).ShouldBe([(ImportIssueCodes.UnreadableWorkbook, null, null)]);
        }

        #endregion

        #region All of the problems

        // The import does not stop at the first problem: it reads everything, and says everything.
        [Fact]
        public void Import_Given_ProblemsInSeveralSheets_Then_AllOfThemAreInTheExceptionInTheOrderOfTheSheets()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Holidays, s => s
                    .Row(1, ("A", TitleOf(nameof(HolidayModel.Id))), ("B", TitleOf(nameof(HolidayModel.StartDateTime))))
                    .Row(2, ("A", NumberOf(1.0)), ("B", TextOf(@"soon"))))
                .Sheet(s_Resources, s => s
                    .Row(1, ("A", TitleOf(nameof(ResourceModel.Id))), ("B", TitleOf(nameof(ResourceModel.UnitCost))))
                    .Row(2, ("A", NumberOf(1.0)), ("B", TextOf(@"cheap"))))
                .Sheet(s_Activities, s => s
                    .Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(nameof(ActivityModel.Duration))), ("C", TitleOf(nameof(ActivityModel.HasNoCost))))
                    .Row(2, ("A", NumberOf(1.0)), ("B", TextOf(@"long")), ("C", TextOf(@"maybe")))
                    .Row(3, ("A", NumberOf(2.0)), ("B", NumberOf(3.0)), ("C", BooleanOf(true)))
                    .Row(4, ("A", NumberOf(3.0)), ("B", NumberOf(2.5))))
                .Build();

            ScenarioImportException failure = ImportFailure(workbook);

            // The sheets in the order in which the import reads them, and the cells of a sheet in the order of rows and columns.
            Places(failure).ShouldBe(
            [
                (ImportIssueCodes.NotAWholeNumber, s_Activities, @"B2"),
                (ImportIssueCodes.NotTrueOrFalse, s_Activities, @"C2"),
                (ImportIssueCodes.NotAWholeNumber, s_Activities, @"B4"),
                (ImportIssueCodes.NotANumber, s_Resources, @"B2"),
                (ImportIssueCodes.NotADate, s_Holidays, @"B2"),
            ]);
            failure.TotalCount.ShouldBe(5);
        }

        [Fact]
        public void Import_Given_AProblem_Then_ItTellsTheSheetTheRowTheColumnTheTitleTheNameOfTheRowAndTheValue()
        {
            byte[] workbook = Activities(s => s
                .Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(nameof(ActivityModel.Name))), ("C", TitleOf(nameof(ActivityModel.Duration))))
                .Row(2, ("A", NumberOf(1.0)), ("B", TextOf(@"Lay the foundations")), ("C", NumberOf(4.0)))
                .Row(3, ("A", NumberOf(2.0)), ("B", TextOf(@"Build the wall")), ("C", TextOf(@"a week"))));

            ImportIssue issue = ImportFailure(workbook).Issues.Single();

            issue.Code.ShouldBe(ImportIssueCodes.NotAWholeNumber);
            issue.Sheet.ShouldBe(s_Activities);
            issue.Row.ShouldBe(3);
            issue.Column.ShouldBe(3);
            issue.Cell.ShouldBe(@"C3");
            issue.ColumnTitle.ShouldBe(nameof(ActivityModel.Duration));
            issue.RowName.ShouldBe(@"Build the wall");
            issue.Value.ShouldBe(@"a week");
            issue.Message.ShouldBe(@"""a week"" is not a whole number.");
        }

        // The row number is the one that Excel shows, and so a row that holds nothing is counted, and so is a row at the top.
        [Fact]
        public void Import_Given_ARowAfterRowsThatHoldNothing_Then_ItsNumberIsTheNumberInTheSheet()
        {
            byte[] workbook = Activities(s => s
                .Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(nameof(ActivityModel.Duration))))
                .Row(2, ("A", NumberOf(1.0)), ("B", NumberOf(2.0)))
                .Row(7, ("A", NumberOf(2.0)), ("B", TextOf(@"none"))));

            Places(ImportFailure(workbook)).ShouldBe([(ImportIssueCodes.NotAWholeNumber, s_Activities, @"B7")]);
        }

        [Fact]
        public void Import_Given_AVeryLongValueOrName_Then_TheIssueHoldsTheStartOfIt()
        {
            string longText = new('x', 500);
            byte[] workbook = Activities(s => s
                .Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(nameof(ActivityModel.Name))), ("C", TitleOf(nameof(ActivityModel.Duration))))
                .Row(2, ("A", NumberOf(1.0)), ("B", TextOf(longText)), ("C", TextOf(longText))));

            ImportIssue issue = ImportFailure(workbook).Issues.Single();

            issue.Value.ShouldBe($@"{longText[..100]}…");
            issue.RowName.ShouldBe($@"{longText[..60]}…");
        }

        // Problems are kept up to a limit, and counted past it, so that a column of ten thousand bad cells is not ten thousand
        // objects.
        [Fact]
        public void Import_Given_MoreProblemsThanTheImportKeeps_Then_ItKeepsTheFirstAndCountsAll()
        {
            byte[] workbook = Activities(s =>
            {
                s.Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(nameof(ActivityModel.Duration))));

                for (int i = 0; i < 10_050; i++)
                {
                    s.Row(i + 2, ("A", NumberOf(i + 1)), ("B", TextOf(@"x")));
                }
            });

            ScenarioImportException failure = ImportFailure(workbook);

            failure.TotalCount.ShouldBe(10_050);
            failure.Issues.Count.ShouldBe(10_000);
            failure.Issues[0].Cell.ShouldBe(@"B2");
            failure.Message.ShouldContain(ImportIssueText.BuildHeader(10_050));
            failure.Message.ShouldEndWith(@"And 10040 more.");
        }

        // The message of the exception is for a host that prints only the message: how many, and the first of them.
        [Fact]
        public void Import_Given_ManyProblems_Then_TheMessageHasTheCountTheFirstTenAndTheNumberOfTheOthers()
        {
            byte[] workbook = Activities(s =>
            {
                s.Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(nameof(ActivityModel.Duration))));

                for (int i = 0; i < 12; i++)
                {
                    s.Row(i + 2, ("A", NumberOf(i + 1)), ("B", TextOf(@"x")));
                }
            });

            ScenarioImportException failure = ImportFailure(workbook);
            string[] lines = failure.Message.Split('\n');

            lines.Length.ShouldBe(12);
            lines[0].ShouldBe(ImportIssueText.BuildHeader(12));
            lines[1].ShouldBe(ImportIssueText.Describe(failure.Issues[0]));
            lines[10].ShouldBe(ImportIssueText.Describe(failure.Issues[9]));
            lines[11].ShouldBe(@"And 2 more.");
        }

        #endregion

        #region The Ids

        [Fact]
        public void Import_Given_TheSameIdInTwoRows_Then_TheSecondRowIsTheProblemAndItSaysWhereTheFirstIs()
        {
            byte[] workbook = Activities(s => s
                .Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(nameof(ActivityModel.Name))))
                .Row(2, ("A", NumberOf(4.0)), ("B", TextOf(@"first")))
                .Row(3, ("A", NumberOf(5.0)), ("B", TextOf(@"other")))
                .Row(4, ("A", NumberOf(4.0)), ("B", TextOf(@"second"))));

            ImportIssue issue = ImportFailure(workbook).Issues.Single();

            (issue.Code, issue.Cell, issue.RowName).ShouldBe((ImportIssueCodes.DuplicateId, @"A4", @"second"));
            issue.Message.ShouldBe(@"The Id 4 is also the Id of row 2.");
        }

        [Fact]
        public void Import_Given_ASheetWithRowsAndNoColumnOfIds_Then_ThatIsOneProblemAboutTheSheet()
        {
            byte[] workbook = Activities(s => s
                .Row(1, ("A", TitleOf(nameof(ActivityModel.Name))), ("B", TitleOf(nameof(ActivityModel.Duration))))
                .Row(2, ("A", TextOf(@"one")), ("B", NumberOf(1.0)))
                .Row(3, ("A", TextOf(@"two")), ("B", NumberOf(2.0))));

            ScenarioImportException failure = ImportFailure(workbook);

            failure.TotalCount.ShouldBe(1);
            ImportIssue issue = failure.Issues.Single();
            (issue.Code, issue.Sheet, issue.ColumnTitle, issue.Cell).ShouldBe((ImportIssueCodes.MissingColumn, s_Activities, nameof(ActivityModel.Id), null));
        }

        [Fact]
        public void Import_Given_ASheetWithNoColumnOfIdsAndNoRows_Then_ThereIsNoProblem()
        {
            byte[] workbook = Activities(s => s.Row(1, ("A", TitleOf(nameof(ActivityModel.Name)))));

            Import(workbook).DependentActivities.ShouldBeEmpty();
        }

        [Fact]
        public void Import_Given_AnErrorOfExcelInTheIdColumn_Then_ItIsTheOnlyProblemOfTheRow()
        {
            byte[] workbook = Activities(s => s
                .Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(nameof(ActivityModel.Name))))
                .Row(2, ("A", ErrorOf(@"#REF!")), ("B", TextOf(@"broken"))));

            ImportIssue issue = ImportFailure(workbook).Issues.Single();

            (issue.Code, issue.Cell, issue.Value).ShouldBe((ImportIssueCodes.ExcelError, @"A2", @"#REF!"));
        }

        #endregion

        #region The titles

        // A title that the importer reads is the title of one column. If two have it, the importer cannot know which to read.
        [Fact]
        public void Import_Given_TwoColumnsWithATitleThatTheImporterReads_Then_TheSecondColumnIsTheProblem()
        {
            byte[] workbook = Activities(s => s
                .Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(nameof(ActivityModel.Name))), ("C", TitleOf(@"NAME")))
                .Row(2, ("A", NumberOf(1.0)), ("B", TextOf(@"a")), ("C", TextOf(@"b"))));

            ImportIssue issue = ImportFailure(workbook).Issues.Single();

            (issue.Code, issue.Cell, issue.ColumnTitle, issue.Message).ShouldBe(
                (ImportIssueCodes.DuplicateColumn, @"C1", @"NAME", @"The title ""NAME"" is also the title of column B."));
        }

        [Fact]
        public void Import_Given_ASheetWithNoRowOfTitles_Then_ThatIsOneProblemAboutTheSheet()
        {
            byte[] workbook = Activities(s => s.Row(2, ("A", NumberOf(1.0)), ("B", TextOf(@"one"))));

            ImportIssue issue = ImportFailure(workbook).Issues.Single();

            (issue.Code, issue.Sheet, issue.Row, issue.Cell).ShouldBe((ImportIssueCodes.MissingHeader, s_Activities, null, null));
        }

        [Fact]
        public void Import_Given_ASheetWhoseFirstTitleIsBlank_Then_ThatIsOneProblemAboutTheSheet()
        {
            byte[] workbook = Activities(s => s
                .Row(1, ("B", TitleOf(nameof(ActivityModel.Id))))
                .Row(2, ("A", TextOf(@"x")), ("B", NumberOf(1.0))));

            Places(ImportFailure(workbook)).ShouldBe([(ImportIssueCodes.MissingHeader, s_Activities, null)]);
        }

        #endregion

        #region The sheets of the General settings

        [Fact]
        public void Import_Given_AnErrorOfExcelInADate_Then_ItIsTheProblem()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_General, s => s
                    .Row(1, ("A", TitleOf(nameof(ProjectScenarioImportModel.ProjectStart))), ("B", TitleOf(nameof(ProjectScenarioImportModel.Today))))
                    .Row(2, ("A", ErrorOf(@"#VALUE!")), ("B", DateOf(new DateTime(2026, 3, 9)))))
                .Build();

            ImportIssue issue = ImportFailure(workbook).Issues.Single();

            (issue.Code, issue.Sheet, issue.Cell).ShouldBe((ImportIssueCodes.ExcelError, s_General, @"A2"));
        }

        [Fact]
        public void Import_Given_ADateOfTheGeneralSheetThatIsNotADate_Then_ItIsTheProblem()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_General, s => s.Table([nameof(ProjectScenarioImportModel.ProjectStart), nameof(ProjectScenarioImportModel.Today)], [@"next month", new DateTime(2026, 3, 9)]))
                .Build();

            ImportIssue issue = ImportFailure(workbook).Issues.Single();

            (issue.Code, issue.Cell, issue.Value).ShouldBe((ImportIssueCodes.NotADate, @"A2", @"next month"));
        }

        #endregion

        #region The trackers

        private static byte[] TrackerWorkbook(Action<XlsxSheetSpec> trackerRows) =>
            new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s.Table([nameof(ActivityModel.Id), nameof(ActivityModel.Name)], [1, @"one"], [2, @"two"]))
                .Sheet(s_ActivityTracker, s =>
                {
                    s.Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(@"0")), ("C", TitleOf(@"1")), ("D", TitleOf(@"2")));
                    trackerRows(s);
                })
                .Build();

        [Fact]
        public void Import_Given_APercentageThatIsNotAWholeNumber_Then_ItIsTheProblem()
        {
            byte[] workbook = TrackerWorkbook(s => s
                .Row(2, ("A", NumberOf(1.0)), ("B", NumberOf(25.0)), ("C", NumberOf(50.5)), ("D", TextOf(@"half"))));

            ScenarioImportException failure = ImportFailure(workbook);

            failure.Issues.Select(x => (x.Code, x.Sheet, x.Cell, x.ColumnTitle)).ShouldBe(
            [
                (ImportIssueCodes.NotAWholeNumber, s_ActivityTracker, @"C2", @"1"),
                (ImportIssueCodes.NotAWholeNumber, s_ActivityTracker, @"D2", @"2"),
            ]);
        }

        [Fact]
        public void Import_Given_ARowForAnActivityTheWorkbookDoesNotHave_Then_ItIsTheProblemAtTheId()
        {
            byte[] workbook = TrackerWorkbook(s => s
                .Row(2, ("A", NumberOf(1.0)), ("B", NumberOf(25.0)))
                .Row(3, ("A", NumberOf(99.0)), ("B", NumberOf(10.0))));

            ImportIssue issue = ImportFailure(workbook).Issues.Single();

            (issue.Code, issue.Sheet, issue.Cell, issue.Value).ShouldBe((ImportIssueCodes.UnknownActivity, s_ActivityTracker, @"A3", @"99"));
        }

        [Fact]
        public void Import_Given_TwoRowsOfATrackerForTheSameActivity_Then_TheSecondRowIsTheProblem()
        {
            byte[] workbook = TrackerWorkbook(s => s
                .Row(2, ("A", NumberOf(1.0)), ("B", NumberOf(25.0)))
                .Row(3, ("A", NumberOf(1.0)), ("B", NumberOf(30.0))));

            Places(ImportFailure(workbook)).ShouldBe([(ImportIssueCodes.DuplicateId, s_ActivityTracker, @"A3")]);
        }

        [Fact]
        public void Import_Given_ARowOfATrackerWithPercentagesAndNoId_Then_ItIsTheProblemAtTheIdCell()
        {
            byte[] workbook = TrackerWorkbook(s => s.Row(2, ("B", NumberOf(25.0))));

            Places(ImportFailure(workbook)).ShouldBe([(ImportIssueCodes.MissingValue, s_ActivityTracker, @"A2")]);
        }

        [Fact]
        public void Import_Given_ATrackerSheetWithRowsAndNoTitleOfTheId_Then_ThatIsOneProblemAboutTheSheet()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s.Table([nameof(ActivityModel.Id)], [1]))
                .Sheet(s_ActivityTracker, s => s
                    .Row(1, ("A", TitleOf(@"Activity")), ("B", TitleOf(@"0")))
                    .Row(2, ("A", NumberOf(1.0)), ("B", NumberOf(25.0))))
                .Build();

            ImportIssue issue = ImportFailure(workbook).Issues.Single();

            (issue.Code, issue.Sheet, issue.ColumnTitle).ShouldBe((ImportIssueCodes.MissingColumn, s_ActivityTracker, nameof(ActivityModel.Id)));
        }

        // An activity that could not be read is not an activity the workbook does not have: its trackers say nothing more.
        [Fact]
        public void Import_Given_AnActivityThatCannotBeReadAndATrackerForIt_Then_OnlyTheActivityIsAProblem()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s
                    .Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(nameof(ActivityModel.Duration))))
                    .Row(2, ("A", NumberOf(1.0)), ("B", TextOf(@"long"))))
                .Sheet(s_ActivityTracker, s => s
                    .Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(@"0")))
                    .Row(2, ("A", NumberOf(1.0)), ("B", NumberOf(25.0)))
                    .Row(3, ("A", NumberOf(7.0)), ("B", NumberOf(25.0))))
                .Build();

            Places(ImportFailure(workbook)).ShouldBe([(ImportIssueCodes.NotAWholeNumber, s_Activities, @"B2")]);
        }

        [Fact]
        public void Import_Given_ATrackerOfAResourceForAnActivityTheWorkbookDoesNotHave_Then_ItIsTheProblemAtTheId()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(s_Activities, s => s.Table([nameof(ActivityModel.Id)], [1]))
                .Sheet(s_Resources, s => s.Table([nameof(ResourceModel.Id), nameof(ResourceModel.Name)], [1, @"a person"]))
                .Sheet(s_ResourceTracker, s => s
                    .Row(1, ("A", TitleOf(nameof(ActivityModel.Id))), ("B", TitleOf(@"0")))
                    .Row(2, ("A", NumberOf(1.0)), ("B", NumberOf(50.0)))
                    .Row(3, ("A", NumberOf(8.0)), ("B", NumberOf(50.0))))
                .Build();

            Places(ImportFailure(workbook)).ShouldBe([(ImportIssueCodes.UnknownActivity, s_ResourceTracker, @"A3")]);
        }

        #endregion

        #region The simulation sheets

        [Fact]
        public void Import_Given_ASettingOfTheSimulationThatIsNotWhatItTakes_Then_ItIsTheProblem()
        {
            byte[] workbook = new XlsxPackageBuilder()
                .Sheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationSettings, s => s
                    .Table([nameof(SimulationSettingsModel.Iterations), nameof(SimulationSettingsModel.Distribution), nameof(SimulationSettingsModel.UseRandomSeed)], [@"many", @"Nonsense", 1.0]))
                .Build();

            ScenarioImportException failure = ImportFailure(workbook);

            failure.Issues.Select(x => (x.Code, x.Cell, x.Value)).ShouldBe(
            [
                (ImportIssueCodes.NotAWholeNumber, @"A2", @"many"),
                (ImportIssueCodes.NotAChoice, @"B2", @"Nonsense"),
                (ImportIssueCodes.NotTrueOrFalse, @"C2", @"1"),
            ]);
        }

        #endregion
    }
}
