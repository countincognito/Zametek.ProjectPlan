using Shouldly;
using System;
using System.IO;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The sheet of a resource tracker in an Excel workbook. A resource tracker can have two entries for one activity on one
    /// day: the application does not write such a tracker, but a program can. The tracking series adds the two entries, and
    /// the workbook does the same. Before, the export failed with "An item with the same key has already been added".
    /// </summary>
    public class XlsxScenarioResourceTrackerTests
    {
        private static readonly TimeProvider s_Clock =
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);

        [Fact]
        public void Export_Given_AResourceTrackerWithTwoEntriesForOneActivity_Then_TheSheetHasTheirSum()
        {
            XlsxSheet sheet = ExportResourceTracker(
            [
                new ResourceActivityTrackerModel { Time = 1, ResourceId = 1, ActivityId = 2, PercentageWorked = 30 },
                new ResourceActivityTrackerModel { Time = 1, ResourceId = 1, ActivityId = 3, PercentageWorked = 10 },
                new ResourceActivityTrackerModel { Time = 1, ResourceId = 1, ActivityId = 2, PercentageWorked = 20 },
            ]);

            Worked(sheet, activityId: 2, time: 1).ShouldBe(50);
            Worked(sheet, activityId: 3, time: 1).ShouldBe(10);
            Worked(sheet, activityId: 1, time: 1).ShouldBeNull();
        }

        [Fact]
        public void Export_Given_AResourceTrackerWithOneEntryForEachActivity_Then_TheSheetHasEachEntry()
        {
            XlsxSheet sheet = ExportResourceTracker(
            [
                new ResourceActivityTrackerModel { Time = 1, ResourceId = 1, ActivityId = 2, PercentageWorked = 30 },
                new ResourceActivityTrackerModel { Time = 1, ResourceId = 1, ActivityId = 3, PercentageWorked = 10 },
            ]);

            Worked(sheet, activityId: 2, time: 1).ShouldBe(30);
            Worked(sheet, activityId: 3, time: 1).ShouldBe(10);
        }

        // The sheet of the tracker of resource 1, which has the entries on day 1.
        private static XlsxSheet ExportResourceTracker(ResourceActivityTrackerModel[] entries)
        {
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(3);
            scenario = scenario with
            {
                ResourceSettings = scenario.ResourceSettings with
                {
                    Resources =
                    [
                        new ResourceModel
                        {
                            Id = 1,
                            Name = @"One",
                            Trackers = [new ResourceTrackerModel { Time = 1, ResourceId = 1, ActivityTrackers = [.. entries] }],
                        },
                    ],
                },
            };

            using var stream = new MemoryStream();
            new XlsxScenarioFileExporter(new DateTimeCalculator(s_Clock))
                .ExportProjectScenarioXlsxFile(scenario, new ResourceSeriesSetModel(), new TrackingSeriesSetModel(), showDates: false, stream);

            return XlsxPackageReader.Read(stream.ToArray())
                .Sheet($@"{Resource.ProjectPlan.Reporting.Reporting_WorksheetResourceTracker} (1)")
                .ShouldNotBeNull();
        }

        // The cell of an activity on a day. Column 1 has the ids of the activities, and the column of day n is n + 2. Row 1
        // has the titles.
        private static double? Worked(XlsxSheet sheet, int activityId, int time)
        {
            int row = Enumerable.Range(2, sheet.LastRow - 1).Single(x => sheet.Cell(x, 1)?.Number == activityId);
            return sheet.Cell(row, time + 2)?.Number;
        }
    }
}
