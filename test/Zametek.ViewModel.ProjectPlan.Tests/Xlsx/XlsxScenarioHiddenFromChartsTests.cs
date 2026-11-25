using Shouldly;
using System;
using System.IO;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The switch that hides an activity, a resource or a work stream from the Gantt chart and the resource chart is a column
    /// of its sheet in a workbook. A workbook from before has no such column, and its import hides nothing. The two switches
    /// "Show Hidden" of the charts are not in the workbook, as no other switch of a chart is (the decision of the user).
    /// </summary>
    public class XlsxScenarioHiddenFromChartsTests
    {
        private const string c_Column = nameof(ActivityModel.IsHiddenFromCharts);

        private static readonly TimeProvider s_Clock =
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);

        [Fact]
        public void Import_Given_AnExportedWorkbook_Then_EachSwitchIsAsItWas()
        {
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(3);
            scenario = scenario with
            {
                DependentActivities = [.. scenario.DependentActivities.Select(x => x with { Activity = x.Activity with { IsHiddenFromCharts = x.Activity.Id == 2 } })],
                ResourceSettings = scenario.ResourceSettings with
                {
                    Resources = [.. scenario.ResourceSettings.Resources.Select(x => x with { IsHiddenFromCharts = x.Id == 1 })],
                },
                WorkStreamSettings = new WorkStreamSettingsModel
                {
                    WorkStreams =
                    [
                        new WorkStreamModel { Id = 1, Name = @"AA", DisplayOrder = 1 },
                        new WorkStreamModel { Id = 2, Name = @"BB", DisplayOrder = 2, IsHiddenFromCharts = true },
                    ],
                },
            };

            ProjectScenarioImportModel imported = Import(Export(scenario));

            imported.DependentActivities.OrderBy(x => x.Activity.Id).Select(x => x.Activity.IsHiddenFromCharts).ShouldBe([false, true, false]);
            imported.ResourceSettings.Resources.OrderBy(x => x.Id).Select(x => x.IsHiddenFromCharts).ShouldBe([true, false]);
            imported.WorkStreams.OrderBy(x => x.Id).Select(x => x.IsHiddenFromCharts).ShouldBe([false, true]);
        }

        [Fact]
        public void Export_Given_AScenario_Then_TheSheetsOfTheActivitiesTheResourcesAndTheWorkStreamsHaveTheColumn()
        {
            XlsxBook book = XlsxPackageReader.Read(Export(CoreViewModelFixture.CreateProjectScenario(3) with
            {
                WorkStreamSettings = new WorkStreamSettingsModel { WorkStreams = [new WorkStreamModel { Id = 1, Name = @"AA" }] },
            }));

            book.Sheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetActivities)!.Header().ShouldContain(c_Column);
            book.Sheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetResources)!.Header().ShouldContain(c_Column);
            book.Sheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetWorkStreams)!.Header().ShouldContain(c_Column);
            book.Sheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetDisplaySettings)!.Header()
                .ShouldNotContain(x => x.EndsWith(@"ShowHidden", StringComparison.Ordinal));
        }

        [Fact]
        public void Import_Given_AWorkbookFromBeforeTheColumn_Then_NothingIsHidden()
        {
            byte[] workbook = File.ReadAllBytes(Path.Combine(@"TestFiles", @"Xlsx", @"docs-sample.xlsx"));
            XlsxBook book = XlsxPackageReader.Read(workbook);
            book.Sheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetActivities)!.Header()
                .ShouldNotContain(c_Column, "precondition: the workbook is from before the column");

            ProjectScenarioImportModel imported = Import(workbook);

            imported.DependentActivities.ShouldNotBeEmpty();
            imported.DependentActivities.ShouldAllBe(x => !x.Activity.IsHiddenFromCharts);
            imported.ResourceSettings.Resources.ShouldNotBeEmpty();
            imported.ResourceSettings.Resources.ShouldAllBe(x => !x.IsHiddenFromCharts);
            imported.WorkStreams.ShouldAllBe(x => !x.IsHiddenFromCharts);
        }

        [Fact]
        public void Import_Given_AScenarioThatShowsTheHiddenParts_Then_TheImportDoesNot()
        {
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(3);
            scenario = scenario with
            {
                DisplaySettings = scenario.DisplaySettings with { GanttChartShowHidden = true, ResourceChartShowHidden = true },
            };

            ProjectScenarioImportModel imported = Import(Export(scenario));

            imported.DisplaySettings.GanttChartShowHidden.ShouldBeFalse();
            imported.DisplaySettings.ResourceChartShowHidden.ShouldBeFalse();
        }

        private static byte[] Export(ProjectScenarioModel scenario)
        {
            using var stream = new MemoryStream();
            new XlsxScenarioFileExporter(new DateTimeCalculator(s_Clock))
                .ExportProjectScenarioXlsxFile(scenario, new ResourceSeriesSetModel(), new TrackingSeriesSetModel(), showDates: false, stream);
            return stream.ToArray();
        }

        private static ProjectScenarioImportModel Import(byte[] workbook)
        {
            using var stream = new MemoryStream(workbook);
            return new XlsxScenarioFileImporter(new DateTimeCalculator(s_Clock)).ImportProjectScenarioXlsxFile(stream);
        }
    }
}
