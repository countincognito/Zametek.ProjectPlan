using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// What the exporter puts into a workbook: the sheets, their titles, the kind of value that each column holds, and how a
    /// date and a title look. They say it of the workbook as its XML holds it, and so they hold for any library that writes it.
    /// A person opens the workbook in Excel, and the importer reads it again: both depend on this.
    /// </summary>
    public class XlsxScenarioExportContentTests
    {
        private static async Task<XlsxBook> ExportAsync(bool showDates)
        {
            var (scenario, resources, tracking) = await XlsxSampleExport.SampleAsync();
            return XlsxPackageReader.Read(XlsxSampleExport.Export(scenario, resources, tracking, showDates));
        }

        private static XlsxSheet Sheet(XlsxBook book, string name) => book.Sheet(name).ShouldNotBeNull();

        [Fact]
        public async Task Export_Given_TheSample_Then_TheSheetsAreTheseInThisOrder()
        {
            var (scenario, resources, tracking) = await XlsxSampleExport.SampleAsync();

            XlsxBook book = XlsxPackageReader.Read(XlsxSampleExport.Export(scenario, resources, tracking, showDates: false));

            // A sheet of trackers for each resource, in the order of the Ids of the resources.
            IEnumerable<string> resourceTrackers = scenario.ResourceSettings.Resources
                .OrderBy(x => x.Id)
                .Select(x => $@"{Resource.ProjectPlan.Reporting.Reporting_WorksheetResourceTracker} ({x.Id})");

            book.SheetNames.ShouldBe(
            [
                Resource.ProjectPlan.Reporting.Reporting_WorksheetGeneral,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetDisplaySettings,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetResourceSettings,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetActivities,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetResources,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetActivitySeverities,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetWorkStreams,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetHolidays,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetActivityTracker,
                .. resourceTrackers,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetResourceChartActivity,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetResourceChartCost,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetResourceChartEffort,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetEarnedValueChartPlan,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetEarnedValueChartEffort,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetEarnedValueChartProgress,
            ]);
        }

        [Fact]
        public async Task Export_Given_TheSample_Then_EveryTitleIsBoldAndTwelvePoints()
        {
            XlsxBook book = await ExportAsync(showDates: false);

            foreach (XlsxSheet sheet in book.Sheets)
            {
                IEnumerable<XlsxCell> titles = sheet.Cells.Where(x => x.Row == 1);

                titles.ShouldNotBeEmpty(sheet.Name);
                titles.ShouldAllBe(x => x.Bold && x.FontSize == 12.0, sheet.Name);
            }
        }

        [Fact]
        public async Task Export_Given_TheSample_Then_TheTitlesOfTheActivitiesAreTheseInThisOrder()
        {
            XlsxBook book = await ExportAsync(showDates: false);

            // The columns that the importer reads by their titles, the columns that are for the reader, and the columns that tell
            // how the activities link, in the order that the exporter gives them.
            Sheet(book, Resource.ProjectPlan.Reporting.Reporting_WorksheetActivities).Header().ShouldBe(
            [
                "Id", "DisplayOrder", "Name", "TargetWorkStreams", "TargetResources", "TargetResourceOperator", "AllocatedToResources",
                "HasNoCost", "HasNoBilling", "HasNoEffort", "HasNoRisk", "Duration", "MostLikelyDuration", "OptimisticDuration",
                "PessimisticDuration", "ExpectedDuration", "StandardDeviation", "FreeSlack", "TotalSlack", "EarliestStartTime",
                "LatestStartTime", "EarliestFinishTime", "LatestFinishTime", "MinimumFreeSlack", "MinimumEarliestStartTime",
                "MinimumEarliestStartDateTime", "MaximumLatestFinishTime", "MaximumLatestFinishDateTime", "OverrideColor", "ColorFormat",
                "Notes", "Dependencies", "PlanningDependencies", "ResourceDependencies", "Successors",
            ]);
        }

        // The kind of value that a column of the first activity holds. The importer reads the text of a cell, and a person who
        // opens the workbook sees a number, a truth value or a text as such.
        [Theory]
        [InlineData("Id", XlsxCellKind.Number)]
        [InlineData("DisplayOrder", XlsxCellKind.Number)]
        [InlineData("Name", XlsxCellKind.Text)]
        [InlineData("TargetResourceOperator", XlsxCellKind.Text)]
        [InlineData("HasNoCost", XlsxCellKind.Boolean)]
        [InlineData("HasNoEffort", XlsxCellKind.Boolean)]
        [InlineData("Duration", XlsxCellKind.Number)]
        [InlineData("ExpectedDuration", XlsxCellKind.Number)]
        [InlineData("StandardDeviation", XlsxCellKind.Number)]
        [InlineData("EarliestStartTime", XlsxCellKind.Number)]
        [InlineData("OverrideColor", XlsxCellKind.Boolean)]
        public async Task Export_Given_TheSample_Then_AColumnOfActivitiesHoldsValuesOfOneKind(string title, XlsxCellKind kind)
        {
            XlsxSheet sheet = Sheet(await ExportAsync(showDates: false), Resource.ProjectPlan.Reporting.Reporting_WorksheetActivities);
            int column = sheet.Header().ToList().IndexOf(title) + 1;

            Enumerable.Range(2, sheet.LastRow - 1)
                .Select(row => sheet.Cell(row, column))
                .ShouldAllBe(x => x != null && x.Kind == kind, title);
        }

        [Fact]
        public async Task Export_Given_TheSample_Then_ADataCellIsNotBold()
        {
            XlsxSheet sheet = Sheet(await ExportAsync(showDates: false), Resource.ProjectPlan.Reporting.Reporting_WorksheetActivities);

            sheet.Cells.Where(x => x.Row > 1).ShouldAllBe(x => !x.Bold && x.FontSize == 11.0);
        }

        [Fact]
        public async Task Export_Given_TheSample_Then_TheIdsOfTheTrackersAreTitlesAndTheTrackersAreWholePercentages()
        {
            XlsxBook book = await ExportAsync(showDates: false);

            foreach (XlsxSheet sheet in book.Sheets.Where(x =>
                x.Name == Resource.ProjectPlan.Reporting.Reporting_WorksheetActivityTracker
                || x.Name.StartsWith(Resource.ProjectPlan.Reporting.Reporting_WorksheetResourceTracker, StringComparison.Ordinal)))
            {
                sheet.Header()[0].ShouldBe("Id", sheet.Name);

                // A row is an activity: its Id leads it, in the look of a title; and there is a cell after it only where there is a tracker, which can be of 0 percent.
                foreach (IGrouping<int, XlsxCell> row in sheet.Cells.Where(x => x.Row > 1).GroupBy(x => x.Row))
                {
                    XlsxCell id = row.First();
                    (id.Column, id.Kind, id.Bold).ShouldBe((1, XlsxCellKind.Number, true), sheet.Name);

                    row.Skip(1).ShouldAllBe(x => x.Kind == XlsxCellKind.Number && x.Number >= 0.0 && x.Number <= 100.0 && x.Number == Math.Floor(x.Number), sheet.Name);
                }

                // The times are the numbers 0, 1, 2... after the Id.
                sheet.Header().Skip(1).ShouldBe(Enumerable.Range(0, sheet.LastColumn - 1).Select(x => x.ToString()), sheet.Name);
            }
        }

        [Fact]
        public async Task Export_Given_TheSample_Then_TheChartsHaveTheirTitlesAndTheirKindsOfValue()
        {
            XlsxBook book = await ExportAsync(showDates: false);

            // The allocations of the resources to the activities, and the efforts, are truth values: a resource is at work or it is not. The costs are numbers.
            XlsxSheet activity = Sheet(book, Resource.ProjectPlan.Reporting.Reporting_WorksheetResourceChartActivity);
            activity.Header()[0].ShouldBe(Resource.ProjectPlan.Reporting.Reporting_Time);
            activity.Cells.Where(x => x.Row > 1 && x.Column > 1).ShouldAllBe(x => x.Kind == XlsxCellKind.Boolean);
            Sheet(book, Resource.ProjectPlan.Reporting.Reporting_WorksheetResourceChartCost).Cells.Where(x => x.Row > 1 && x.Column > 1).ShouldAllBe(x => x.Kind == XlsxCellKind.Number);
            Sheet(book, Resource.ProjectPlan.Reporting.Reporting_WorksheetResourceChartEffort).Cells.Where(x => x.Row > 1 && x.Column > 1).ShouldAllBe(x => x.Kind == XlsxCellKind.Boolean);

            // The time runs down the first column from 0, a day to a row, in the look of a title.
            activity.Cells.Where(x => x.Row > 1 && x.Column == 1).Select(x => x.Number).ShouldBe(Enumerable.Range(0, activity.LastRow - 1).Select(x => (double)x));
            activity.Cells.Where(x => x.Row > 1 && x.Column == 1).ShouldAllBe(x => x.Bold);

            XlsxSheet plan = Sheet(book, Resource.ProjectPlan.Reporting.Reporting_WorksheetEarnedValueChartPlan);
            plan.Header().ShouldBe(["Time", "ActivityId", "ActivityName", "Value", "ValuePercentage"]);
            plan.Cells.Where(x => x.Row > 1 && x.Column is 1 or 2 or 4 or 5).ShouldAllBe(x => x.Kind == XlsxCellKind.Number);
        }

        [Fact]
        public async Task Export_Given_NoDates_Then_TheTimesAreWholeNumbersOfDays()
        {
            XlsxBook book = await ExportAsync(showDates: false);
            XlsxSheet activities = Sheet(book, Resource.ProjectPlan.Reporting.Reporting_WorksheetActivities);
            int column = activities.Header().ToList().IndexOf("EarliestStartTime") + 1;

            activities.Cell(2, column)!.ShouldSatisfyAllConditions(x => x.Kind.ShouldBe(XlsxCellKind.Number), x => x.Number.ShouldBe(0.0));
            Sheet(book, Resource.ProjectPlan.Reporting.Reporting_WorksheetActivityTracker).Cell(@"B1")!.Kind.ShouldBe(XlsxCellKind.Number);

            // The two dates of the plan are dates whatever the choice.
            Sheet(book, Resource.ProjectPlan.Reporting.Reporting_WorksheetGeneral).Cells.Where(x => x.Row == 2).ShouldAllBe(x => x.Kind == XlsxCellKind.Date);
        }

        [Fact]
        public async Task Export_Given_Dates_Then_TheTimesAreDatesInTheFormatOfTheShortDate()
        {
            XlsxBook book = await ExportAsync(showDates: true);
            XlsxSheet activities = Sheet(book, Resource.ProjectPlan.Reporting.Reporting_WorksheetActivities);
            int column = activities.Header().ToList().IndexOf("EarliestStartTime") + 1;
            double start = new DateTime(2026, 1, 5).ToOADate();

            // The plan starts on 5 January 2026, and its first activity starts on the first day.
            activities.Cell(2, column)!.ShouldSatisfyAllConditions(
                x => x.Kind.ShouldBe(XlsxCellKind.Date),
                x => x.Number.ShouldBe(start),
                x => x.FormatCode.ShouldBe(DateTimeCalculator.DateFormat));

            // The times of a sheet of trackers are the dates of the days, and the first is the day that the plan starts.
            XlsxSheet tracker = Sheet(book, Resource.ProjectPlan.Reporting.Reporting_WorksheetActivityTracker);
            tracker.Cell(@"B1")!.ShouldSatisfyAllConditions(x => x.Kind.ShouldBe(XlsxCellKind.Date), x => x.Number.ShouldBe(start), x => x.Bold.ShouldBeTrue());
            tracker.Cell(@"C1")!.Number.ShouldBe(start + 1);

            // And so are those of the charts.
            Sheet(book, Resource.ProjectPlan.Reporting.Reporting_WorksheetResourceChartActivity).Cell(@"A2")!.ShouldSatisfyAllConditions(
                x => x.Kind.ShouldBe(XlsxCellKind.Date),
                x => x.Number.ShouldBe(start));
        }

        [Fact]
        public async Task Export_Given_TheSample_Then_TheSettingsOfTheDisplayAreTruthValuesAndTheNameOfTheMode()
        {
            XlsxSheet display = Sheet(await ExportAsync(showDates: false), Resource.ProjectPlan.Reporting.Reporting_WorksheetDisplaySettings);

            display.Header().ShouldBe(["ShowDates", "UseClassicDates", "NonWorkingDayMode", "HideCost", "HideBilling"]);
            display.Cells.Where(x => x.Row == 2).Select(x => x.Kind).ShouldBe(
                [XlsxCellKind.Boolean, XlsxCellKind.Boolean, XlsxCellKind.Text, XlsxCellKind.Boolean, XlsxCellKind.Boolean]);
        }

        // A date of the plan is the day that it shows: a start in the zone +10 is the day it is there, and not the day it is in UTC.
        [Fact]
        public void Export_Given_DatesWithAnOffsetFromUtc_Then_TheCellsHoldTheTimeOnTheClockOfTheDate()
        {
            var scenario = new ProjectScenarioModel
            {
                ProjectStart = new DateTimeOffset(2026, 3, 9, 0, 0, 0, TimeSpan.FromHours(10)),
                Today = new DateTimeOffset(2026, 3, 12, 0, 0, 0, TimeSpan.FromHours(-7)),
            };

            XlsxSheet general = Sheet(
                XlsxPackageReader.Read(XlsxSampleExport.Export(scenario, new ResourceSeriesSetModel(), new TrackingSeriesSetModel(), showDates: false)),
                Resource.ProjectPlan.Reporting.Reporting_WorksheetGeneral);

            DateTime.FromOADate(general.Cell(@"A2")!.Number).ShouldBe(new DateTime(2026, 3, 9));
            DateTime.FromOADate(general.Cell(@"B2")!.Number).ShouldBe(new DateTime(2026, 3, 12));
        }

        [Fact]
        public async Task Export_Given_TheSample_Then_ADecimalNumberIsANumberAndNotText()
        {
            XlsxSheet resources = Sheet(await ExportAsync(showDates: false), Resource.ProjectPlan.Reporting.Reporting_WorksheetResources);
            int column = resources.Header().ToList().IndexOf("UnitCost") + 1;

            resources.Cell(2, column)!.ShouldSatisfyAllConditions(x => x.Kind.ShouldBe(XlsxCellKind.Number), x => x.Number.ShouldBe(700.0));
        }
    }
}
