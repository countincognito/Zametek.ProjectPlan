using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The settings and the result of the schedule simulation in an Excel workbook. A workbook that is exported and
    /// imported again must give back the settings, and the result with its lists, as they were. A scenario that has
    /// nothing to say about the simulation gives the workbook that it gave before the simulation existed.
    /// </summary>
    public class XlsxScenarioSimulationTests
    {
        private static readonly TimeProvider s_Clock =
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);

        // A value of its own in every member, so that one that is not carried across shows. The third weight of the first bin
        // is a number that a double cannot hold exactly in decimal (0.1 + 0.2), and the seed is the widest that there is.
        private static SimulationSettingsModel Settings() =>
            new()
            {
                Iterations = 1234,
                Distribution = DurationDistribution.ModifiedBetaPert,
                ModifiedBetaShape = 6.5,
                Seed = ulong.MaxValue - 3,
                UseRandomSeed = false,
                TargetFinishTime = 77,
            };

        private static SimulationSummaryModel Summary() =>
            new()
            {
                IterationCount = 1234,
                ValidIterationCount = 1200,
                FailedIterationCount = 34,
                PlannedFinishTime = 60,
                TargetFinishTime = 77,
                TargetProbability = 0.875,
                Mean = 66.25,
                StandardDeviation = 4.5,
                Minimum = 50,
                Maximum = 90,
                Percentiles =
                [
                    new SimulationPercentileModel { Percentile = 50, FinishTime = 66 },
                    new SimulationPercentileModel { Percentile = 90, FinishTime = 73 },
                ],
                BinWidth = 2,
                Bins =
                [
                    new SimulationHistogramBinModel { FirstFinishTime = 50, LastFinishTime = 51, Count = 10, CumulativeShare = 0.25, PathGroupWeights = [6.5, 3.5, 0.1 + 0.2] },
                    new SimulationHistogramBinModel { FirstFinishTime = 52, LastFinishTime = 53, Count = 30, CumulativeShare = 1.0, PathGroupWeights = [20.0, 10.0, 0.0] },
                ],
                Paths =
                [
                    new SimulationPathModel { Rank = 1, Label = "Design > Build ~> Test", ShortLabel = "Design > Build ~> Test", IdLabel = "1 > 3 ~> 7", HasResourceLinks = true, Weight = 700.5, Share = 0.5837 },
                    new SimulationPathModel { Rank = 2, Label = "Design > Test", ShortLabel = "Design > Test", IdLabel = "1 > 7", HasResourceLinks = false, Weight = 300.0, Share = 0.25 },
                ],
                ChartPathCount = 2,
                OtherPathsWeight = 199.5,
                Activities =
                [
                    new SimulationActivityCriticalityModel { ActivityId = 1, Name = "Design", CriticalIterationCount = 1200, CriticalityIndex = 1.0 },
                    new SimulationActivityCriticalityModel { ActivityId = 3, Name = "Build, with a comma", CriticalIterationCount = 700, CriticalityIndex = 0.5833 },
                ],
            };

        private static ProjectScenarioModel Scenario(SimulationSettingsModel? settings = null, SimulationSummaryModel? summary = null) =>
            CoreViewModelFixture.CreateProjectScenario(3) with
            {
                SimulationSettings = settings ?? new SimulationSettingsModel(),
                SimulationSummary = summary,
            };

        [Fact]
        public void Export_Given_TheDefaultSettingsAndNoResult_Then_TheWorkbookHasNoSheetOfTheSimulation()
        {
            // The workbook that every plan gave before the simulation: nothing is new in it.
            using XSSFWorkbook workbook = Open(Export(Scenario()));

            SheetNames(workbook).ShouldNotContain(x => x.StartsWith(@"Simulation", StringComparison.Ordinal));
        }

        [Fact]
        public void Export_Given_ASettingThatIsNotTheDefault_Then_TheWorkbookHasTheSettingsSheetAndNoResult()
        {
            using XSSFWorkbook workbook = Open(Export(Scenario(Settings())));

            SheetNames(workbook).Where(x => x.StartsWith(@"Simulation", StringComparison.Ordinal)).ShouldBe(
                [Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationSettings]);

            ISheet sheet = workbook.GetSheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationSettings);
            HeaderOf(sheet).ShouldBe(["Iterations", "Distribution", "ModifiedBetaShape", "Seed", "UseRandomSeed", "TargetFinishTime"]);
            IRow row = sheet.GetRow(1);
            row.GetCell(0).NumericCellValue.ShouldBe(1234.0);
            row.GetCell(1).StringCellValue.ShouldBe(nameof(DurationDistribution.ModifiedBetaPert));
            row.GetCell(2).NumericCellValue.ShouldBe(6.5);

            // The seed is text, because a cell holds only 53 bits of a whole number.
            row.GetCell(3).CellType.ShouldBe(CellType.String);
            row.GetCell(3).StringCellValue.ShouldBe((ulong.MaxValue - 3).ToString(CultureInfo.InvariantCulture));
            row.GetCell(4).BooleanCellValue.ShouldBeFalse();
            row.GetCell(5).NumericCellValue.ShouldBe(77.0);
        }

        [Fact]
        public void Export_Given_AResult_Then_TheWorkbookHasTheSheetsOfTheSimulationWithTheirColumns()
        {
            using XSSFWorkbook workbook = Open(Export(Scenario(Settings(), Summary())));

            SheetNames(workbook).Where(x => x.StartsWith(@"Simulation", StringComparison.Ordinal)).ShouldBe(
            [
                Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationSettings,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationStatistics,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationPercentiles,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationHistogram,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationPaths,
                Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationCriticalities,
            ]);

            HeaderOf(workbook.GetSheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationPercentiles)).ShouldBe(["Percentile", "FinishTime"]);
            HeaderOf(workbook.GetSheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationHistogram)).ShouldBe(
                ["FirstFinishTime", "LastFinishTime", "Count", "CumulativeShare", "PathGroupWeight1", "PathGroupWeight2", "PathGroupWeight3"]);
            HeaderOf(workbook.GetSheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationPaths)).ShouldBe(
                ["Rank", "Label", "ShortLabel", "IdLabel", "HasResourceLinks", "Weight", "Share"]);
            HeaderOf(workbook.GetSheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationCriticalities)).ShouldBe(
                ["ActivityId", "Name", "CriticalIterationCount", "CriticalityIndex"]);

            // One row for each item of a list, under the titles.
            workbook.GetSheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationPercentiles).LastRowNum.ShouldBe(2);
            workbook.GetSheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationHistogram).LastRowNum.ShouldBe(2);
            workbook.GetSheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationPaths).LastRowNum.ShouldBe(2);
            workbook.GetSheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationCriticalities).LastRowNum.ShouldBe(2);
        }

        [Fact]
        public void Import_Given_AnExportedWorkbook_Then_TheSettingsAreAsTheyWere()
        {
            ProjectScenarioImportModel import = Import(Export(Scenario(Settings(), Summary())));

            import.SimulationSettings.ShouldBe(Settings());
        }

        [Fact]
        public void Import_Given_AnExportedWorkbook_Then_TheResultIsAsItWas()
        {
            SimulationSummaryModel expected = Summary();

            SimulationSummaryModel? summary = Import(Export(Scenario(Settings(), expected))).SimulationSummary;

            summary.ShouldNotBeNull();
            (summary.IterationCount, summary.ValidIterationCount, summary.FailedIterationCount, summary.PlannedFinishTime, summary.TargetFinishTime, summary.TargetProbability)
                .ShouldBe((1234, 1200, 34, 60, 77, 0.875));
            (summary.Mean, summary.StandardDeviation, summary.Minimum, summary.Maximum, summary.BinWidth, summary.ChartPathCount, summary.OtherPathsWeight)
                .ShouldBe((66.25, 4.5, 50, 90, 2, 2, 199.5));
            summary.Percentiles.ShouldBe(expected.Percentiles);
            summary.Paths.ShouldBe(expected.Paths);
            summary.Activities.ShouldBe(expected.Activities);
            summary.Bins.Select(x => (x.FirstFinishTime, x.LastFinishTime, x.Count, x.CumulativeShare))
                .ShouldBe(expected.Bins.Select(x => (x.FirstFinishTime, x.LastFinishTime, x.Count, x.CumulativeShare)));

            // Each weight is the very double that was written, and not a rounded one.
            summary.Bins[0].PathGroupWeights.ShouldBe([6.5, 3.5, 0.1 + 0.2]);
            summary.Bins[1].PathGroupWeights.ShouldBe([20.0, 10.0, 0.0]);

            // The critical paths as activity ids are what the Critical paths table shows, and a workbook has them.
            summary.Paths.Select(x => x.IdLabel).ShouldBe(["1 > 3 ~> 7", "1 > 7"]);
            summary.HasResult.ShouldBeTrue();
        }

        [Fact]
        public void Import_Given_NoTargetAndNoChanceOfMeetingIt_Then_BothAreNullAndNotZero()
        {
            SimulationSettingsModel settings = Settings() with { TargetFinishTime = null };
            SimulationSummaryModel summary = Summary() with { TargetFinishTime = null, TargetProbability = null };

            ProjectScenarioImportModel import = Import(Export(Scenario(settings, summary)));

            import.SimulationSettings.TargetFinishTime.ShouldBeNull();
            import.SimulationSummary!.TargetFinishTime.ShouldBeNull();
            import.SimulationSummary.TargetProbability.ShouldBeNull();
        }

        [Theory]
        [InlineData(0UL)]
        [InlineData(1UL)]
        [InlineData(9007199254740993UL)] // 2^53 + 1: the first whole number that a double cannot hold.
        [InlineData(ulong.MaxValue)]
        public void Import_Given_ASeed_Then_ItIsExactToTheLastBit(ulong seed)
        {
            ProjectScenarioImportModel import = Import(Export(Scenario(Settings() with { Seed = seed })));

            import.SimulationSettings.Seed.ShouldBe(seed);
        }

        [Theory]
        [InlineData(@"de-DE")]
        [InlineData(@"fr-FR")]
        [InlineData(@"en-US")]
        [InlineData(@"tr-TR")]
        public void Import_Given_AnyCulture_Then_TheNumbersAreTheSameAsInAnother(string culture)
        {
            // A person who makes the workbook where a comma is the decimal mark, and a person who opens it where a point is,
            // must see the same numbers.
            CultureInfo previous = CultureInfo.CurrentCulture;
            ProjectScenarioImportModel import;

            try
            {
                CultureInfo.CurrentCulture = new CultureInfo(culture);
                byte[] workbook = Export(Scenario(Settings(), Summary()));
                CultureInfo.CurrentCulture = new CultureInfo(@"ja-JP");
                import = Import(workbook);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }

            import.SimulationSettings.ShouldBe(Settings());
            import.SimulationSummary!.Mean.ShouldBe(66.25);
            import.SimulationSummary.Bins[0].PathGroupWeights[2].ShouldBe(0.1 + 0.2);
            import.SimulationSummary.Paths[0].Share.ShouldBe(0.5837);
        }

        [Fact]
        public void Import_Given_AWorkbookWithNoSheetOfTheSimulation_Then_TheSettingsAreTheDefaultsAndThereIsNoResult()
        {
            ProjectScenarioImportModel import = Import(Export(Scenario()));

            import.SimulationSettings.ShouldBe(new SimulationSettingsModel());
            import.SimulationSummary.ShouldBeNull();
        }

        [Fact]
        public void Import_Given_TheSettingsAndNoStatistics_Then_TheSettingsAreKeptAndThereIsNoResult()
        {
            // A result is more than its lists: without the statistics there is none.
            byte[] bytes = Edit(
                Export(Scenario(Settings(), Summary())),
                workbook => workbook.RemoveSheetAt(workbook.GetSheetIndex(Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationStatistics)));

            ProjectScenarioImportModel import = Import(bytes);

            import.SimulationSettings.ShouldBe(Settings());
            import.SimulationSummary.ShouldBeNull();
        }

        [Fact]
        public void Import_Given_TheStatisticsAndNoLists_Then_TheResultHasTheStatisticsAndNoRows()
        {
            byte[] bytes = Edit(
                Export(Scenario(Settings(), Summary())),
                workbook =>
                {
                    workbook.RemoveSheetAt(workbook.GetSheetIndex(Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationHistogram));
                    workbook.RemoveSheetAt(workbook.GetSheetIndex(Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationPaths));
                });

            SimulationSummaryModel? summary = Import(bytes).SimulationSummary;

            summary.ShouldNotBeNull();
            summary.Mean.ShouldBe(66.25);
            summary.Bins.ShouldBeEmpty();
            summary.Paths.ShouldBeEmpty();
            summary.Percentiles.Count.ShouldBe(2);
            summary.Activities.Count.ShouldBe(2);
        }

        [Fact]
        public void Import_Given_ACellThatHoldsNoValueOfItsKind_Then_ThatSettingIsTheDefaultAndTheOthersAreRead()
        {
            byte[] bytes = Edit(
                Export(Scenario(Settings())),
                workbook =>
                {
                    IRow row = workbook.GetSheet(Resource.ProjectPlan.Reporting.Reporting_WorksheetSimulationSettings).GetRow(1);
                    row.GetCell(0).SetCellValue(@"many");          // Iterations
                    row.GetCell(1).SetCellValue(@"NotADistribution"); // Distribution
                    row.GetCell(3).SetCellValue(@"-5");            // Seed
                });

            SimulationSettingsModel settings = Import(bytes).SimulationSettings;

            SimulationSettingsModel defaults = new();
            settings.Iterations.ShouldBe(defaults.Iterations);
            settings.Distribution.ShouldBe(defaults.Distribution);
            settings.Seed.ShouldBe(defaults.Seed);
            settings.ModifiedBetaShape.ShouldBe(6.5);
            settings.UseRandomSeed.ShouldBeFalse();
            settings.TargetFinishTime.ShouldBe(77);
        }

        [Fact]
        public void Export_Given_TheSameResultAtTheSameTime_Then_TheWorkbooksAreIdentical()
        {
            ProjectScenarioModel scenario = Scenario(Settings(), Summary());

            Export(scenario).ShouldBe(Export(scenario));
        }

        [Fact]
        public void ProcessProjectScenarioImport_Given_AWorkbookWithASimulation_Then_TheScenarioHoldsItAndIsMarkedAsNotYetSaved()
        {
            ProjectScenarioImportModel import = Import(Export(Scenario(Settings(), Summary())));
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.GraphSettings = CoreViewModelFixture.DefaultGraphSettings;

            core.ProcessProjectScenarioImport(import, Guid.NewGuid(), @"Imported");

            core.SimulationSettings.ShouldBe(Settings());
            core.SimulationSummary.ShouldBeSameAs(import.SimulationSummary);
            core.SimulationSummary.ShouldNotBeNull();
            core.IsProjectScenarioUpdated.ShouldBeTrue();
        }

        [Fact]
        public void ProcessProjectScenarioImport_Given_AWorkbookWithoutOne_Then_TheScenarioHasTheDefaultsAndNoResult()
        {
            // A workbook that is imported over a scenario that has a result does not keep the result of the scenario.
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.GraphSettings = CoreViewModelFixture.DefaultGraphSettings;
            core.ProcessProjectScenarioImport(Import(Export(Scenario(Settings(), Summary()))), Guid.NewGuid(), @"First");
            core.SimulationSummary.ShouldNotBeNull();

            core.ProcessProjectScenarioImport(Import(Export(Scenario())), Guid.NewGuid(), @"Second");

            core.SimulationSettings.ShouldBe(new SimulationSettingsModel());
            core.SimulationSummary.ShouldBeNull();
        }

        #region Helpers

        private static byte[] Export(ProjectScenarioModel scenario)
        {
            var calculator = new DateTimeCalculator(s_Clock);
            using var stream = new MemoryStream();
            new XlsxScenarioFileExporter(calculator)
                .ExportProjectScenarioXlsxFile(scenario, new ResourceSeriesSetModel(), new TrackingSeriesSetModel(), showDates: false, stream);

            return stream.ToArray();
        }

        private static ProjectScenarioImportModel Import(byte[] workbook)
        {
            using var stream = new MemoryStream(workbook);
            return new XlsxScenarioFileImporter(new DateTimeCalculator(s_Clock)).ImportProjectScenarioXlsxFile(stream);
        }

        private static XSSFWorkbook Open(byte[] workbook) => new(new MemoryStream(workbook));

        // Opens the workbook, changes it, and writes it again, as a person does who works on it in Excel.
        private static byte[] Edit(byte[] workbook, Action<XSSFWorkbook> change)
        {
            using XSSFWorkbook opened = Open(workbook);
            change(opened);

            using var output = new MemoryStream();
            opened.Write(output, leaveOpen: true);
            return output.ToArray();
        }

        private static List<string> SheetNames(IWorkbook workbook) =>
            [.. Enumerable.Range(0, workbook.NumberOfSheets).Select(workbook.GetSheetName)];

        // The titles of the first row of a sheet, in the order of its columns.
        private static List<string> HeaderOf(ISheet sheet) =>
            [.. Enumerable.Range(0, sheet.GetRow(0)?.LastCellNum ?? 0).Select(i => sheet.GetRow(0).GetCell(i)?.ToString() ?? string.Empty)];

        #endregion
    }
}
