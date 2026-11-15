using Shouldly;
using System;
using Xunit;
using Zametek.Common.ProjectPlan;
using static Zametek.ViewModel.ProjectPlan.Tests.SequencerScopes;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The three subscriptions that do the automatic work of the core stay alive after an exception: the automatic
    /// compilation, the build of the outputs after a compilation, and the build of the risk metrics. An exception that
    /// leaves a handler ends its subscription for the rest of the session, and then no later edit compiles. The handler
    /// writes the exception to the log, and it marks the outputs as stale.
    /// </summary>
    public class CoreViewModelSubscriptionFaultTests
    {
        [Fact]
        public void BuildCascade_Given_AnOutputThatFails_Then_TheNextCompilationStillBuildsItsOutputs()
        {
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            (GatedCoreViewModel core, FaultyMetricCalculationService metrics) = CoreViewModelFixture.CreateGatedWithFaultyMetrics();
            using GatedCoreViewModel _ = core;
            Start(core, main, pool);
            metrics.NetworkFailure = new InvalidOperationException(@"The metric failed.");

            core.RunCompile();
            Should.NotThrow(() => Drain(main, pool));

            core.HasStaleOutputs.ShouldBeTrue(@"the outputs do not match the plan");
            int revision = core.CompilationOutputRevision;

            metrics.NetworkFailure = null;
            core.RunCompile();
            Drain(main, pool);

            core.CompilationOutputRevision.ShouldBe(revision + 1, @"the subscription that builds the outputs has ended");
            core.HasStaleOutputs.ShouldBeFalse();
        }

        [Fact]
        public void BuildRiskMetrics_Given_ABuildThatFails_Then_TheNextChangeOfTheSettingsStillBuilds()
        {
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            (GatedCoreViewModel core, FaultyMetricCalculationService metrics) = CoreViewModelFixture.CreateGatedWithFaultyMetrics();
            using GatedCoreViewModel _ = core;
            Start(core, main, pool);
            core.RunCompile();
            Drain(main, pool);
            metrics.RiskFailure = new InvalidOperationException(@"The metric failed.");

            core.GraphSettings = InANewList(core.GraphSettings);
            Should.NotThrow(() => Drain(main, pool));

            core.HasStaleOutputs.ShouldBeTrue(@"the risk metrics do not match the settings");

            metrics.RiskFailure = null;
            int builds = metrics.RiskBuilds;
            core.GraphSettings = InANewList(core.GraphSettings);
            Drain(main, pool);

            metrics.RiskBuilds.ShouldBeGreaterThan(builds, @"the subscription that builds the risk metrics has ended");
        }

        [Fact]
        public void AutoCompile_Given_ACompilationThatFails_Then_TheNextEditStillCompiles()
        {
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            (GatedCoreViewModel core, FaultyMetricCalculationService _) = CoreViewModelFixture.CreateGatedWithFaultyMetrics();
            using GatedCoreViewModel __ = core;
            Start(core, main, pool);
            core.AutoCompile = true;
            Drain(main, pool);
            core.FailNextCompilation(new InvalidOperationException(@"The compilation failed."));

            core.RequestedProjectStart = core.RequestedProjectStart.AddDays(7);
            Should.NotThrow(() => Drain(main, pool));

            core.HasStaleOutputs.ShouldBeTrue(@"the outputs do not match the plan");
            core.AutoCompile.ShouldBeTrue(@"only a compilation that runs out of time switches automatic compilation off");
            int started = core.CompilationsStarted;

            core.RequestedProjectStart = core.RequestedProjectStart.AddDays(7);
            Drain(main, pool);

            core.CompilationsStarted.ShouldBeGreaterThan(started, @"the subscription that compiles has ended");
            core.HasStaleOutputs.ShouldBeFalse();
        }

        // Starts the subscriptions of a core that has a plan of three activities.
        private static void Start(GatedCoreViewModel core, MainThreadSequencerScope main, TaskpoolSequencerScope pool)
        {
            core.StartSubscriptions();
            core.RequestedProjectStart = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
            core.GraphSettings = CoreViewModelFixture.DefaultGraphSettings;
            core.AddManagedActivities(
            [
                new DependentActivityModel { Activity = new ActivityModel { Id = 1, DisplayOrder = 1, Duration = 10 } },
                new DependentActivityModel { Activity = new ActivityModel { Id = 2, DisplayOrder = 2, Duration = 6 } },
                new DependentActivityModel { Activity = new ActivityModel { Id = 3, DisplayOrder = 3, Duration = 5 } },
            ]);
            Drain(main, pool);
        }

        // The same settings in a new list, so that the core takes them as a change.
        private static GraphSettingsModel InANewList(GraphSettingsModel settings) =>
            settings with { ActivitySeverities = [.. settings.ActivitySeverities] };
    }
}
