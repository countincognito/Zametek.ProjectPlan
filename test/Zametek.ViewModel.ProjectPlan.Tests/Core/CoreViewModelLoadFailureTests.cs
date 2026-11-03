using Microsoft.Extensions.Logging;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// What a load that does not end leaves behind. The core then holds a part of a scenario, and the scenario manager writes
    /// what the core holds over the scenario that the id names. Thus, the id must name no scenario until the core holds all of
    /// it. A compilation that outlasts its budget is not such a failure: the scenario is whole, and only its outputs are missing.
    /// </summary>
    public class CoreViewModelLoadFailureTests
    {
        private static readonly Guid s_FirstId = new(@"6f1f7e0a-4c55-4a57-8a0e-0f1d3b7a9c11");
        private static readonly Guid s_SecondId = new(@"a3c1d2e4-7b66-4f08-9c21-5e8f2a6d4b22");

        private const double c_Criticality = 0.25;
        private const int c_Iterations = 1234;

        private static ProjectScenarioModel Scenario() =>
            CoreViewModelFixture.CreateProjectScenario(3) with
            {
                DisplaySettings = new ProjectScenarioDisplaySettingsModel
                {
                    ArrowGraphEdgeRoutingMode = EdgeRoutingMode.Rectilinear,
                },
                Metrics = new MetricsModel { Risks = new RisksModel { Criticality = c_Criticality } },
                ResourceMetrics = [new ResourceMetricsModel { ResourceId = 1, ResourceName = @"Resource 1" }],
                SimulationSettings = new SimulationSettingsModel { Iterations = c_Iterations },
                SimulationSummary = new SimulationSummaryModel { IterationCount = c_Iterations },
            };

        // A load fails on this at the activities, when the settings of the scenario are in place.
        private static ProjectScenarioModel BrokenScenario() =>
            Scenario() with { DependentActivities = null! };

        private const int c_ImportedActivityCount = 5;
        private const int c_ImportedIterations = 4321;

        private static ProjectScenarioImportModel Import()
        {
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(c_ImportedActivityCount);

            return new ProjectScenarioImportModel
            {
                ProjectStart = scenario.ProjectStart,
                Today = scenario.Today,
                DependentActivities = scenario.DependentActivities,
                ResourceSettings = scenario.ResourceSettings,
                SimulationSettings = new SimulationSettingsModel { Iterations = c_ImportedIterations },
            };
        }

        private static void ImportShouldBeWhole(ProjectScenarioModel saved)
        {
            saved.DependentActivities.Count.ShouldBe(c_ImportedActivityCount);
            saved.ResourceSettings.Resources.Count.ShouldBe(2);
            saved.SimulationSettings.Iterations.ShouldBe(c_ImportedIterations);
        }

        private static int[] ActivityIds(ICoreViewModel core) =>
            [.. core.BuildProjectScenario().DependentActivities.Select(x => x.Activity.Id).Order()];

        private static void ScenarioShouldBeWhole(ProjectScenarioModel saved)
        {
            saved.DependentActivities.Count.ShouldBe(3);
            saved.ResourceSettings.Resources.Count.ShouldBe(2);
            saved.DisplaySettings.ArrowGraphEdgeRoutingMode.ShouldBe(EdgeRoutingMode.Rectilinear);
            saved.Metrics.Risks.Criticality.ShouldBe(c_Criticality);
            saved.ResourceMetrics.ShouldHaveSingleItem().ResourceName.ShouldBe(@"Resource 1");
            saved.SimulationSettings.Iterations.ShouldBe(c_Iterations);
            saved.SimulationSummary.ShouldNotBeNull().IterationCount.ShouldBe(c_Iterations);
        }

        [Fact]
        public void ProcessProjectScenario_Given_AScenarioThatFails_Then_TheIdNamesNoScenario()
        {
            (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
            using CoreViewModel coreOwner = core;
            core.ProcessProjectScenario(Scenario(), s_FirstId, @"First");

            Should.Throw<Exception>(() => core.ProcessProjectScenario(BrokenScenario(), s_SecondId, @"Second"));

            settings.ScenarioId.ShouldNotBe(s_FirstId);
            settings.ScenarioId.ShouldNotBe(s_SecondId);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void ProcessProjectScenario_Given_AChangeThatFails_Then_TheIdNamesNoScenario(int failingChange)
        {
            (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
            using CoreViewModel coreOwner = core;
            core.ProcessProjectScenario(Scenario(), s_FirstId, @"First");

            using (new RoutingModeFault(core, failingChange))
            {
                Should.Throw<InvalidOperationException>(() => core.ProcessProjectScenario(Scenario(), s_SecondId, @"Second"));
            }

            settings.ScenarioId.ShouldNotBe(s_FirstId);
            settings.ScenarioId.ShouldNotBe(s_SecondId);
        }

        [Fact]
        public void ProcessProjectScenario_Given_AFailureAndThenALoad_Then_TheIdIsTheIdOfTheLoad()
        {
            (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
            using CoreViewModel coreOwner = core;
            Should.Throw<Exception>(() => core.ProcessProjectScenario(BrokenScenario(), s_FirstId, @"First"));

            core.ProcessProjectScenario(Scenario(), s_SecondId, @"Second");

            settings.ScenarioId.ShouldBe(s_SecondId);
            ScenarioShouldBeWhole(core.BuildProjectScenario());
        }

        [Fact]
        public void ProcessProjectScenario_Given_AScenarioThatFails_Then_TheFailureIsLogged()
        {
            var logger = new RecordingLogger();
            (CoreViewModel core, _) = CoreViewModelFixture.CreateWithSettingService(logger: logger);
            using CoreViewModel coreOwner = core;

            Exception thrown = Should.Throw<Exception>(() => core.ProcessProjectScenario(BrokenScenario(), s_FirstId, @"First"));

            (string message, Exception? exception) = logger.Errors.ShouldHaveSingleItem();
            message.ShouldContain(@"First");
            message.ShouldContain(s_FirstId.ToString());
            exception.ShouldBeSameAs(thrown);
        }

        [Fact]
        public void ResetProjectScenario_Given_AChangeThatFails_Then_TheIdNamesNoScenario()
        {
            (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
            using CoreViewModel coreOwner = core;
            core.ProcessProjectScenario(Scenario(), s_FirstId, @"First");

            using (new RoutingModeFault(core, 1))
            {
                Should.Throw<InvalidOperationException>(core.ResetProjectScenario);
            }

            settings.ScenarioId.ShouldNotBe(s_FirstId);
        }

        [Fact]
        public void ProcessProjectScenarioImport_Given_AnImportThatFails_Then_TheIdNamesNoScenario()
        {
            (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
            using CoreViewModel coreOwner = core;
            core.ProcessProjectScenario(Scenario(), s_FirstId, @"First");

            Should.Throw<Exception>(() => core.ProcessProjectScenarioImport(
                new ProjectScenarioImportModel { DependentActivities = null! },
                s_FirstId,
                @"First"));

            settings.ScenarioId.ShouldNotBe(s_FirstId);
        }

        [Fact]
        public void ProcessProjectScenario_Given_ACompilationThatOutlastsTheWatchdog_Then_TheScenarioIsWholeAndHasItsId()
        {
            (GatedCoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateGatedWithSettingService(compilationTimeoutMilliseconds: 1_000);
            using GatedCoreViewModel coreOwner = core;
            core.AutoCompile = true;
            core.StallNextCompilation();

            Should.Throw<GraphCompilationTimeoutException>(() => core.ProcessProjectScenario(Scenario(), s_FirstId, @"First"));

            settings.ScenarioId.ShouldBe(s_FirstId);
            ScenarioShouldBeWhole(core.BuildProjectScenario());
            core.HasStaleOutputs.ShouldBeTrue(@"the plan has no outputs");
            core.AutoCompile.ShouldBeFalse(@"the next edit must not walk back into the same hang");
            core.IsProjectScenarioUpdated.ShouldBeFalse(@"a load is not an edit");
            core.IsBusy.ShouldBeFalse();
            core.IsBulkUpdating.ShouldBeFalse();
        }

        [Fact]
        public void ProcessProjectScenario_Given_ACompilationThatOutlastsTheWatchdog_Then_ItIsLoggedOnce()
        {
            var logger = new RecordingLogger();
            (GatedCoreViewModel core, _) = CoreViewModelFixture.CreateGatedWithSettingService(compilationTimeoutMilliseconds: 1_000, logger: logger);
            using GatedCoreViewModel coreOwner = core;
            core.StallNextCompilation();

            Exception thrown = Should.Throw<GraphCompilationTimeoutException>(() => core.ProcessProjectScenario(Scenario(), s_FirstId, @"First"));

            logger.Errors.ShouldHaveSingleItem().Exception.ShouldBeSameAs(thrown);
        }

        [Fact]
        public void ProcessProjectScenario_Given_ACompilationThatOutlastsTheWatchdogAndThenALoad_Then_TheOutputsAreNotStale()
        {
            (GatedCoreViewModel core, _) = CoreViewModelFixture.CreateGatedWithSettingService(compilationTimeoutMilliseconds: 1_000);
            using GatedCoreViewModel coreOwner = core;
            core.StallNextCompilation();
            Should.Throw<GraphCompilationTimeoutException>(() => core.ProcessProjectScenario(Scenario(), s_FirstId, @"First"));

            core.ProcessProjectScenario(Scenario(), s_SecondId, @"Second");

            core.HasStaleOutputs.ShouldBeFalse();
        }

        // An import keeps its scenario whole in the same way. It takes the place of the scenario on show, and so it is an edit
        // of that scenario.
        [Fact]
        public void ProcessProjectScenarioImport_Given_ACompilationThatOutlastsTheWatchdog_Then_TheImportIsWholeAndHasItsId()
        {
            (GatedCoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateGatedWithSettingService(compilationTimeoutMilliseconds: 1_000);
            using GatedCoreViewModel coreOwner = core;
            core.ProcessProjectScenario(Scenario(), s_FirstId, @"First");
            core.AutoCompile = true;
            core.StallNextCompilation();

            Should.Throw<GraphCompilationTimeoutException>(() => core.ProcessProjectScenarioImport(Import(), s_FirstId, @"First"));

            settings.ScenarioId.ShouldBe(s_FirstId);
            settings.ScenarioTitle.ShouldBe(@"First");
            ImportShouldBeWhole(core.BuildProjectScenario());
            core.HasStaleOutputs.ShouldBeTrue(@"the plan has no outputs");
            core.AutoCompile.ShouldBeFalse(@"the next edit must not walk back into the same hang");
            core.IsProjectScenarioUpdated.ShouldBeTrue(@"an import is an edit");
            core.IsBusy.ShouldBeFalse();
            core.IsBulkUpdating.ShouldBeFalse();
        }

        [Fact]
        public void UpdateManagedActivityIds_Given_AChangeThatFails_Then_TheScenarioGoesBackToWhatItWas()
        {
            (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
            using CoreViewModel coreOwner = core;
            core.ProcessProjectScenario(Scenario(), s_FirstId, @"First");

            using (new RoutingModeFault(core, 1))
            {
                Should.Throw<InvalidOperationException>(() => core.UpdateManagedActivityIds([(2, 5)])).Message.ShouldBe(RoutingModeFault.Message);
            }

            settings.ScenarioId.ShouldBe(s_FirstId);
            settings.ScenarioTitle.ShouldBe(@"First");
            ActivityIds(core).ShouldBe([1, 2, 3]);
            ScenarioShouldBeWhole(core.BuildProjectScenario());
            core.IsProjectScenarioUpdated.ShouldBeTrue();
        }

        [Fact]
        public void UpdateManagedActivityIds_Given_AChangeThatFailsAndAWayBackThatFails_Then_TheIdNamesNoScenario()
        {
            (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
            using CoreViewModel coreOwner = core;
            core.ProcessProjectScenario(Scenario(), s_FirstId, @"First");

            using (new RoutingModeFault(core, 1, 2))
            {
                Should.Throw<InvalidOperationException>(() => core.UpdateManagedActivityIds([(2, 5)]));
            }

            settings.ScenarioId.ShouldNotBe(s_FirstId);
        }

        [Fact]
        public void UpdateManagedActivityIds_Given_ACompilationThatOutlastsTheWatchdog_Then_TheScenarioHasTheNewIds()
        {
            (GatedCoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateGatedWithSettingService(compilationTimeoutMilliseconds: 1_000);
            using GatedCoreViewModel coreOwner = core;
            core.ProcessProjectScenario(Scenario(), s_FirstId, @"First");
            core.StallNextCompilation();

            Should.Throw<GraphCompilationTimeoutException>(() => core.UpdateManagedActivityIds([(2, 5)]));

            settings.ScenarioId.ShouldBe(s_FirstId);
            settings.ScenarioTitle.ShouldBe(@"First");
            ActivityIds(core).ShouldBe([1, 3, 5]);
            core.HasStaleOutputs.ShouldBeTrue();
            core.IsProjectScenarioUpdated.ShouldBeTrue(@"the new ids are an edit");
        }

        // Remembers what the core logs as an error.
        private sealed class RecordingLogger
            : ILogger<CoreViewModel>
        {
            public List<(string Message, Exception? Exception)> Errors { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Error)
                {
                    Errors.Add((formatter(state, exception), exception));
                }
            }
        }
    }
}
