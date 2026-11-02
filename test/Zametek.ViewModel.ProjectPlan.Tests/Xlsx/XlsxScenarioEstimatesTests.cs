using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The three estimates of an activity's duration in an Excel workbook. A workbook that is exported
    /// and imported again must give back each activity with the estimates that its user gave it,
    /// and no more: an estimate that was not given is a blank cell, and it comes back as null.
    /// </summary>
    public class XlsxScenarioEstimatesTests
    {
        private static readonly TimeProvider s_Clock =
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);

        [Fact]
        public void Import_Given_AnExportedWorkbook_Then_EachActivityKeepsTheEstimatesItWasGiven()
        {
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(3) with
            {
                DependentActivities =
                [
                    Activity(1, duration: 9, mostLikely: 10, optimistic: 4, pessimistic: null),
                    Activity(2, duration: 6, mostLikely: 6, optimistic: null, pessimistic: null),
                    Activity(3, duration: 7, mostLikely: null, optimistic: null, pessimistic: null),
                    Activity(4, duration: 12, mostLikely: 10, optimistic: 4, pessimistic: 22),
                ],
            };

            List<ActivityModel> imported = RoundTrip(scenario);

            imported.Count.ShouldBe(4);
            Shape(imported.Single(x => x.Id == 1)).ShouldBe((9, 10, 4, (int?)null));
            Shape(imported.Single(x => x.Id == 2)).ShouldBe((6, 6, (int?)null, (int?)null));
            Shape(imported.Single(x => x.Id == 3)).ShouldBe((7, (int?)null, (int?)null, (int?)null));
            Shape(imported.Single(x => x.Id == 4)).ShouldBe((12, 10, 4, 22));
        }

        [Fact]
        public void Import_Given_AWorkbookOfAPlanWithNoEstimates_Then_NoActivityHasAnyAndTheDurationsAreAsTheyWere()
        {
            // The workbook that every plan gave before the estimates: nothing in the new columns.
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(5);

            List<ActivityModel> imported = RoundTrip(scenario);

            imported.Select(x => x.Duration).ShouldBe(scenario.DependentActivities.Select(x => x.Activity.Duration));
            imported.ShouldAllBe(x => x.MostLikelyDuration == null
                && x.OptimisticDuration == null
                && x.PessimisticDuration == null);
        }

        [Fact]
        public void Export_Given_ActivitiesWithEstimates_Then_TheWorkbookHasTheExpectedDurationAndTheStandardDeviationOfEach()
        {
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(3) with
            {
                DependentActivities =
                [
                    // (4 + 40 + 10) / 6 = 9, and (10 - 4) / 6 = 1.
                    Activity(1, duration: 9, mostLikely: 10, optimistic: 4, pessimistic: null),
                    // No estimates: the expected duration is the duration, and there is no spread.
                    Activity(2, duration: 6, mostLikely: null, optimistic: null, pessimistic: null),
                    // (4 + 40 + 22 + 3) / 6 = 11 (a half goes up), and (22 - 4) / 6 = 3.
                    Activity(3, duration: 11, mostLikely: 10, optimistic: 4, pessimistic: 22),
                ],
            };
            var calculator = new DateTimeCalculator(s_Clock);
            using var stream = new MemoryStream();
            new XlsxScenarioFileExporter(calculator)
                .ExportProjectScenarioXlsxFile(scenario, new ResourceSeriesSetModel(), new TrackingSeriesSetModel(), showDates: false, stream);

            XlsxSheet sheet = XlsxPackageReader.Read(stream.ToArray()).Sheets.Single(x => x.Header().Contains(@"ExpectedDuration"));
            List<string> header = [.. sheet.Header()];

            // The columns sit with the estimates that they come from.
            header.IndexOf(@"ExpectedDuration").ShouldBe(header.IndexOf(@"PessimisticDuration") + 1);
            header.IndexOf(@"StandardDeviation").ShouldBe(header.IndexOf(@"ExpectedDuration") + 1);

            // The rows under the titles: row 2 of the sheet is the first of the activities.
            var rows = Enumerable.Range(2, 3)
                .ToDictionary(
                    row => (int)sheet.Cell(row, header.IndexOf(nameof(ActivityModel.Id)) + 1)!.Number,
                    row => (
                        Expected: sheet.Cell(row, header.IndexOf(@"ExpectedDuration") + 1)!.Number,
                        StandardDeviation: sheet.Cell(row, header.IndexOf(@"StandardDeviation") + 1)!.Number));

            rows[1].ShouldBe((9.0, 1.0));
            rows[2].ShouldBe((6.0, 0.0));
            rows[3].ShouldBe((11.0, 3.0));
        }

        [Fact]
        public void Import_Given_AWorkbookThatHasTheComputedColumns_Then_TheyAreIgnored()
        {
            // The two columns are for the reader. A workbook that is exported with them and imported
            // again gives the same activities as one that never had them.
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(1) with
            {
                DependentActivities = [Activity(1, duration: 9, mostLikely: 10, optimistic: 4, pessimistic: null)],
            };

            ActivityModel imported = RoundTrip(scenario).Single();

            Shape(imported).ShouldBe((9, 10, 4, (int?)null));
        }

        [Fact]
        public void Import_Given_AnImportedActivity_Then_TheApplicationSchedulesItsExpectedDuration()
        {
            // The whole way through: the workbook, the import, and the activity that the application builds.
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(1) with
            {
                DependentActivities = [Activity(1, duration: 99, mostLikely: 10, optimistic: 4, pessimistic: null)],
            };
            ProjectScenarioImportModel import = Import(scenario);

            using CoreViewModel core = CoreViewModelFixture.Create();
            core.GraphSettings = CoreViewModelFixture.DefaultGraphSettings;
            core.AddManagedActivities(import.DependentActivities);

            // (4 + 40 + 10) / 6 = 9, and not the 99 that the Duration cell said.
            core.RawActivities.Single().Duration.ShouldBe(9);
        }

        #region Helpers

        private static DependentActivityModel Activity(int id, int duration, int? mostLikely, int? optimistic, int? pessimistic) =>
            new()
            {
                Activity = new ActivityModel
                {
                    Id = id,
                    DisplayOrder = id,
                    Name = $@"Activity {id}",
                    Duration = duration,
                    MostLikelyDuration = mostLikely,
                    OptimisticDuration = optimistic,
                    PessimisticDuration = pessimistic,
                },
            };

        private static (int Duration, int? MostLikely, int? Optimistic, int? Pessimistic) Shape(ActivityModel activity) =>
            (activity.Duration, activity.MostLikelyDuration, activity.OptimisticDuration, activity.PessimisticDuration);

        private static ProjectScenarioImportModel Import(ProjectScenarioModel scenario)
        {
            var calculator = new DateTimeCalculator(s_Clock);
            using var stream = new MemoryStream();
            new XlsxScenarioFileExporter(calculator)
                .ExportProjectScenarioXlsxFile(scenario, new ResourceSeriesSetModel(), new TrackingSeriesSetModel(), showDates: false, stream);

            stream.Position = 0;
            return new XlsxScenarioFileImporter(calculator).ImportProjectScenarioXlsxFile(stream);
        }

        private static List<ActivityModel> RoundTrip(ProjectScenarioModel scenario) =>
            [.. Import(scenario).DependentActivities.Select(x => x.Activity)];

        #endregion
    }
}
