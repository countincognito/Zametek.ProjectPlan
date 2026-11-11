using Shouldly;
using System;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    // A plan that a program writes can leave out the graph settings of a scenario: docs/PROJECT-FILE.md says that zpp gives a
    // member that a plan leaves out its default. The criticality risk takes the weight of the most critical severity, and with
    // no severities the load of the scenario failed ("Sequence contains no elements"). A scenario now gets the default list
    // in place of each list of the graph settings that it does not have, as an import does.
    public class CoreViewModelGraphSettingsTests
    {
        [Fact]
        public void ProcessProjectScenario_Given_NoGraphSettings_Then_TheDefaultOnesAndTheRiskMetrics()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(6) with { GraphSettings = new GraphSettingsModel() };

            core.ProcessProjectScenario(scenario, Guid.NewGuid(), @"No graph settings");

            // As a job does after it loads a scenario: it compiles it, and builds its metrics.
            core.RunCompile();
            core.BuildRiskMetrics();

            GraphSettingsModel defaults = CoreViewModelFixture.DefaultGraphSettings;
            core.GraphSettings.ActivitySeverities.ShouldBe(defaults.ActivitySeverities);
            core.GraphSettings.EdgeTypeFormats.ShouldBe(defaults.EdgeTypeFormats);
            core.GraphSettings.NodeTypeFormats.ShouldBe(defaults.NodeTypeFormats);
            core.HasCompilationErrors.ShouldBeFalse();
            core.Metrics.Risks.Criticality.ShouldNotBeNull();
        }

        [Fact]
        public void ProcessProjectScenario_Given_SeveritiesAndNoFormats_Then_KeepsTheSeveritiesAndTakesTheDefaultFormats()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            ActivitySeverityModel severity = new() { SlackLimit = int.MaxValue, CriticalityWeight = 2.0, FibonacciWeight = 3.0, ColorFormat = ColorHelper.Random() };
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(6) with
            {
                GraphSettings = new GraphSettingsModel { ActivitySeverities = [severity] },
            };

            core.ProcessProjectScenario(scenario, Guid.NewGuid(), @"Severities only");

            core.GraphSettings.ActivitySeverities.ShouldBe([severity]);
            core.GraphSettings.EdgeTypeFormats.ShouldBe(CoreViewModelFixture.DefaultGraphSettings.EdgeTypeFormats);
            core.GraphSettings.NodeTypeFormats.ShouldBe(CoreViewModelFixture.DefaultGraphSettings.NodeTypeFormats);
        }
    }
}
