using Shouldly;
using System;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A run of the simulation belongs to one scenario. The run reads the revision of the scenario before it starts, and it
    /// gives the core its result together with that revision. The core compares the revision and writes the result in one
    /// step, under its lock. So a scenario that loads or resets while the run ends does not get the result of the scenario
    /// before it, which the next save would write to the file.
    /// </summary>
    public class CoreViewModelSimulationSummaryTests
    {
        private static readonly SimulationSummaryModel s_Summary = new() { IterationCount = 10, ValidIterationCount = 10 };

        [Fact]
        public void TrySetSimulationSummary_Given_TheRevisionOfTheScenario_Then_TheScenarioHasTheSummary()
        {
            using CoreViewModel core = Load();
            int revision = core.ProjectScenarioRevision;
            core.IsProjectScenarioUpdated = false;

            bool written = core.TrySetSimulationSummary(s_Summary, revision);

            written.ShouldBeTrue();
            core.SimulationSummary.ShouldBeSameAs(s_Summary);
            core.IsProjectScenarioUpdated.ShouldBeTrue();
        }

        [Fact]
        public void TrySetSimulationSummary_Given_AScenarioThatResetSinceTheRevision_Then_NothingIsWritten()
        {
            using CoreViewModel core = Load();
            int revision = core.ProjectScenarioRevision;
            core.ResetProjectScenario();
            core.ProjectScenarioRevision.ShouldNotBe(revision);
            core.IsProjectScenarioUpdated = false;

            bool written = core.TrySetSimulationSummary(s_Summary, revision);

            written.ShouldBeFalse();
            core.SimulationSummary.ShouldBeNull();
            core.IsProjectScenarioUpdated.ShouldBeFalse();
        }

        [Fact]
        public void TrySetSimulationSummary_Given_AScenarioThatLoadedSinceTheRevision_Then_TheSummaryOfThatScenarioStays()
        {
            using CoreViewModel core = Load();
            int revision = core.ProjectScenarioRevision;
            var summaryOfTheFile = new SimulationSummaryModel { IterationCount = 20, ValidIterationCount = 20 };
            core.ProcessProjectScenario(
                CoreViewModelFixture.CreateProjectScenario(3) with { SimulationSummary = summaryOfTheFile },
                Guid.NewGuid(),
                @"Other");

            bool written = core.TrySetSimulationSummary(s_Summary, revision);

            written.ShouldBeFalse();
            core.SimulationSummary.ShouldNotBeNull();
            core.SimulationSummary.IterationCount.ShouldBe(20);
        }

        private static CoreViewModel Load()
        {
            CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(3), Guid.NewGuid(), @"Plan");
            return core;
        }
    }
}
