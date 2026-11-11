using Shouldly;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    // The panel of the scenario chart saves the data of the chart as the two CSV files that zpp writes with the chart, next
    // to the name that the user chooses, as the panel of the simulation saves its files.
    public class ScenarioChartManagerViewModelTests
    {
        private sealed class Harness
            : IDisposable
        {
            private Harness(CoreViewModel core, ProjectScenarioManagerViewModel manager, SimulationChartManagerViewModelTests.RecordingDialogService dialogs, ScenarioChartManagerViewModel panel)
            {
                Core = core;
                Manager = manager;
                Dialogs = dialogs;
                Panel = panel;
            }

            public CoreViewModel Core { get; }

            public ProjectScenarioManagerViewModel Manager { get; }

            public SimulationChartManagerViewModelTests.RecordingDialogService Dialogs { get; }

            public ScenarioChartManagerViewModel Panel { get; }

            public static Harness Make()
            {
                (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
                var manager = new ProjectScenarioManagerViewModel(
                    core,
                    settings,
                    CoreViewModelFixture.CreateDialogService(),
                    new DateTimeCalculator(TimeProvider.System),
                    CoreViewModelFixture.CreateUIDispatcher());
                var dialogs = new SimulationChartManagerViewModelTests.RecordingDialogService();
                var panel = new ScenarioChartManagerViewModel(
                    core,
                    manager,
                    settings,
                    dialogs,
                    new DateTimeCalculator(TimeProvider.System),
                    new ScottPlotImageExporter(),
                    CoreViewModelFixture.CreateUIDispatcher());

                return new Harness(core, manager, dialogs, panel);
            }

            public void Dispose()
            {
                Panel.Dispose();
                Manager.Dispose();
                Core.Dispose();
            }
        }

        [Fact]
        public void BuildScenarioChartData_Given_TheSettingsOfThePlan_Then_TheDataHasTheirAxesAndFits()
        {
            using Harness harness = Harness.Make();
            harness.Panel.BuildScenarioChartData().ShouldBeNull();

            harness.Manager.ScenarioChartTrackedMetricXAxis = TrackedMetrics.NetworkDuration;
            harness.Manager.ScenarioChartTrackedMetricY2Axis = TrackedMetrics.CostsTotal;
            harness.Manager.ScenarioChartCurveFittingTypeY2 = CurveFittingType.Power;
            harness.Manager.ScenarioChartAbsoluteCurveFittingY2 = true;

            ScenarioChartDataModel data = harness.Panel.BuildScenarioChartData().ShouldNotBeNull();

            data.XMetric.ShouldBe(TrackedMetrics.NetworkDuration);
            data.Y1.ShouldBeNull();
            ScenarioChartSeriesModel y2 = data.Y2.ShouldNotBeNull();
            (y2.Metric, y2.CurveFittingType, y2.AbsoluteCurveFitting).ShouldBe((TrackedMetrics.CostsTotal, CurveFittingType.Power, true));
            y2.Points.ShouldBeEmpty();
        }

        [Fact]
        public async Task ExportCsvFilesCommand_Given_AChart_Then_WritesThePointsAndTheFitsNextToTheNameThatTheUserChose()
        {
            using Harness harness = Harness.Make();
            harness.Manager.ScenarioChartTrackedMetricXAxis = TrackedMetrics.NetworkDuration;
            harness.Manager.ScenarioChartTrackedMetricY1Axis = TrackedMetrics.CostsDirect;
            harness.Manager.ScenarioChartCurveFittingTypeY1 = CurveFittingType.PolynomialOrder2;

            string directory = Path.Combine(Path.GetTempPath(), $@"scenario-chart-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                harness.Dialogs.SaveFilename = Path.Combine(directory, @"plan.csv");

                harness.Panel.ExportCsvFilesCommand.Execute(null);

                DateTime deadline = DateTime.UtcNow.AddSeconds(60);
                while (harness.Dialogs.Infos.Count == 0 && harness.Dialogs.Errors.Count == 0 && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(20, CancellationToken.None);
                }

                harness.Dialogs.Errors.ShouldBeEmpty();
                harness.Dialogs.Infos.ShouldBe([string.Format(Resource.ProjectPlan.Messages.Message_ScenarioChartFilesSaved, 2, directory)]);
                Directory.GetFiles(directory).Select(Path.GetFileName).Order().ShouldBe([@"plan-fits.csv", @"plan-points.csv"]);

                // UTF-8 with a byte order mark, as the files of the simulation are. The plan has no tracked scenario.
                File.ReadAllBytes(Path.Combine(directory, @"plan-points.csv")).Take(3).ShouldBe([(byte)0xEF, (byte)0xBB, (byte)0xBF]);
                File.ReadAllLines(Path.Combine(directory, @"plan-points.csv")).ShouldBe([@"Axis,Id,Path,X,Y"]);
                File.ReadAllLines(Path.Combine(directory, @"plan-fits.csv")).ShouldBe(
                [
                    @"Axis,XMetric,YMetric,CurveFitting,Absolute,RSquared,Coefficients,DerivativeCoefficients",
                    @"Y1,NetworkDuration,DirectCost,PolynomialOrder2,false,,,",
                ]);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public async Task ExportCsvFilesCommand_Given_TheUserChoosesNoFile_Then_WritesNothing()
        {
            using Harness harness = Harness.Make();
            harness.Dialogs.SaveFilename = null;

            harness.Panel.ExportCsvFilesCommand.Execute(null);
            await Task.Delay(200, CancellationToken.None);

            harness.Dialogs.Infos.ShouldBeEmpty();
            harness.Dialogs.Errors.ShouldBeEmpty();
        }
    }
}
