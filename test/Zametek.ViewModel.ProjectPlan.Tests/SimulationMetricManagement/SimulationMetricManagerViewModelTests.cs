using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System;
using System.Threading.Tasks;
using Xunit;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The dock "Simulation Metrics": it is the shared simulation seen as three tables, and it feeds nothing back.
    /// </summary>
    public class SimulationMetricManagerViewModelTests
    {
        private sealed class Harness
            : IDisposable
        {
            private readonly CoreViewModel m_Core;
            private readonly SimulationChartManagerViewModel m_Simulation;

            private Harness(
                CoreViewModel core,
                SimulationChartManagerViewModel simulation,
                SimulationChartManagerViewModelTests.RecordingDialogService dialogs,
                SimulationMetricManagerViewModel metrics)
            {
                m_Core = core;
                m_Simulation = simulation;
                Dialogs = dialogs;
                Metrics = metrics;
            }

            public SimulationChartManagerViewModelTests.RecordingDialogService Dialogs { get; }

            public SimulationChartManagerViewModel Simulation => m_Simulation;

            public SimulationMetricManagerViewModel Metrics { get; }

            public static Harness Make()
            {
                (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
                var dialogs = new SimulationChartManagerViewModelTests.RecordingDialogService();

                var simulation = new SimulationChartManagerViewModel(
                    core,
                    settings,
                    dialogs,
                    new DateTimeCalculator(TimeProvider.System),
                    new ScottPlotImageExporter(),
                    CoreViewModelFixture.CreateUIDispatcher(),
                    NullLogger<SimulationChartManagerViewModel>.Instance);

                var metrics = new SimulationMetricManagerViewModel(simulation, dialogs);

                return new Harness(core, simulation, dialogs, metrics);
            }

            public void Dispose()
            {
                Metrics.Dispose();
                m_Simulation.Dispose();
                m_Core.Dispose();
            }
        }

        [Fact]
        public void Construction_Given_ADock_Then_ItIsTheDockSimulationMetricsOverTheSharedSimulation()
        {
            using Harness harness = Harness.Make();

            harness.Metrics.Title.ShouldBe(Resource.ProjectPlan.Titles.Title_SimulationMetrics);
            harness.Metrics.Id.ShouldBe(Resource.ProjectPlan.Titles.Title_SimulationMetrics);
            harness.Metrics.Simulation.ShouldBeSameAs(harness.Simulation);
        }

        [Fact]
        public void Construction_Given_TheThreeDocks_Then_EachHasAnIdOfItsOwn()
        {
            using Harness harness = Harness.Make();
            using var control = new SimulationControlManagerViewModel(
                harness.Simulation,
                DoNothingProxy.Of<IProjectScenarioManagerViewModel>(),
                CoreViewModelFixture.CreateSettingService());

            // The dock layout keys a tool by its id, and a saved layout from before these docks has none of the three.
            string[] ids = [harness.Simulation.Id, control.Id, harness.Metrics.Id];

            ids.ShouldBeUnique();
            ids.ShouldAllBe(x => !string.IsNullOrWhiteSpace(x));
            harness.Simulation.Title.ShouldBe(Resource.ProjectPlan.Titles.Title_SimulationChart);
        }

        [Fact]
        public async Task ReportErrorAsync_Given_AMessage_Then_ShowsItAsAnError()
        {
            using Harness harness = Harness.Make();

            await harness.Metrics.ReportErrorAsync("The clipboard did not take it.");

            harness.Dialogs.Errors.ShouldBe(["The clipboard did not take it."]);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ReportErrorAsync_Given_NoMessage_Then_Throws(string message)
        {
            using Harness harness = Harness.Make();

            await Should.ThrowAsync<ArgumentException>(() => harness.Metrics.ReportErrorAsync(message));

            harness.Dialogs.Errors.ShouldBeEmpty();
        }
    }
}
