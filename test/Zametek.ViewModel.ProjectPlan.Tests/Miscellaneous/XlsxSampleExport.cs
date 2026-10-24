using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The workbooks that the exporter makes of the sample plan, made the same way each time: at a time that is fixed, in a
    /// zone that is fixed, and in a culture that is fixed. The tests of the exporter and the files that hold what the exporter
    /// made (see the folder TestFiles/Xlsx) are made with this.
    /// </summary>
    internal static class XlsxSampleExport
    {
        // The culture that the sample workbooks are made in. The format of the cells that hold dates is the short date of the
        // culture that the process had when the exporter first met a date (DateTimeCalculator.DateFormat is set once), and so
        // a comparison of two workbooks leaves it out.
        public const string Culture = @"en-GB";

        public static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

        // The clock stands still, and is in the zone of the machine: the calculator that writes dates makes a date of the local
        // kind, and that needs the offset of the machine's own zone. The workbooks that the tests compare do not hold the clock's
        // time, and a date in one is the same in any zone.
        public static TimeProvider Clock { get; } = new FixedTimeProvider(Now, TimeZoneInfo.Local);

        // The sample plan, compiled, with the series that the charts and the sheets of the exporter are made from. The plan starts
        // at a time with no offset, in the zone of the clock, so that the dates that it gives are the same on any machine.
        public static async Task<(ProjectScenarioModel Scenario, ResourceSeriesSetModel Resources, TrackingSeriesSetModel Tracking)> SampleAsync()
        {
            ProjectScenarioModel loaded = await CoreViewModelFixture.LoadProjectScenarioAsync(@"sample_v0_6_1.zpp");
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.GraphSettings = CoreViewModelFixture.DefaultGraphSettings;
            core.ProcessProjectScenario(loaded with { ProjectStart = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero), Today = new DateTimeOffset(2026, 1, 19, 0, 0, 0, TimeSpan.Zero) }, Guid.NewGuid(), @"Sample");
            core.RunCompile();
            core.BuildResourceSeriesSet();
            core.BuildTrackingSeriesSet();
            return (core.BuildProjectScenario(), core.ResourceSeriesSet, core.TrackingSeriesSet);
        }

        // A plan with a result of the simulation, and settings of it that are not the defaults.
        public static ProjectScenarioModel WithSimulation(ProjectScenarioModel scenario) =>
            scenario with
            {
                SimulationSettings = new SimulationSettingsModel
                {
                    Iterations = 1234,
                    Distribution = Zametek.Maths.Graphs.DurationDistribution.ModifiedBetaPert,
                    ModifiedBetaShape = 6.5,
                    Seed = ulong.MaxValue - 3,
                    UseRandomSeed = false,
                    TargetFinishTime = 77,
                },
                // The run that made the result used other values than the settings have now: the person changed them after it.
                SimulationSummary = new SimulationSummaryModel
                {
                    IterationCount = 1234,
                    Seed = ulong.MaxValue - 9,
                    Distribution = Zametek.Maths.Graphs.DurationDistribution.ModifiedBetaPert,
                    ModifiedBetaShape = 3.25,
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
                },
            };

        // The workbook that the exporter makes, in the fixed culture.
        public static byte[] Export(ProjectScenarioModel scenario, ResourceSeriesSetModel resources, TrackingSeriesSetModel tracking, bool showDates)
        {
            CultureInfo previous = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = new CultureInfo(Culture);
                using var stream = new MemoryStream();
                new XlsxScenarioFileExporter(new DateTimeCalculator(Clock))
                    .ExportProjectScenarioXlsxFile(scenario, resources, tracking, showDates, stream);
                return stream.ToArray();
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }
    }
}
