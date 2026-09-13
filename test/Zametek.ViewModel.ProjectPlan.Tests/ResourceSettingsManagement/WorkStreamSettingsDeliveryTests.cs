using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A resource's inter-activity phases have to survive a scenario load however the
    /// scheduler chooses to interleave the deliveries that load produces.
    /// </summary>
    /// <remarks>
    /// They did not. Loading a project wrote the resource settings and the work stream
    /// settings twice each - the reset cleared both, then the scenario supplied both -
    /// and those four writes fed two independent deferred subscriptions. Because the
    /// resource settings queue was scheduled first it drained in full, correctly
    /// rebuilding every resource, before the work stream queue delivered its first and
    /// by then stale empty snapshot; that snapshot reconciled each resource's phases
    /// against no work streams at all and discarded them, and the real value arriving
    /// behind it could not restore a selection that had already been destroyed. The
    /// loss then reached the saved file on the next edit.
    /// <para>
    /// The invariant worth pinning is therefore not "phases survive a load" but "phases
    /// survive a load <em>whatever order the deliveries arrive in</em>", which is what
    /// the first two tests assert between them: one drains after the whole load, as a
    /// busy user interface thread would, and one drains after every write, as an idle
    /// one would. The first is the arrangement that failed.
    /// </para>
    /// </remarks>
    public class WorkStreamSettingsDeliveryTests
    {
        private const int c_PhaseWorkStreamId = 1;
        private const int c_ResourceId = 7;

        [Fact]
        public void ResourcePhases_SurviveALoadDrainedAfterEveryWrite()
        {
            using var scope = new MainThreadSequencerScope();
            using CoreViewModelFixture.SubscribedHarness harness = CoreViewModelFixture.CreateWithSubscriptions();

            // An idle user interface thread: every delivery is taken before the next
            // write happens, so no snapshot is ever stale on arrival.
            WriteLoadSequence(harness.Core, () => scope.Pump.Drain());

            AssertPhasesIntact(harness);
        }

        [Fact]
        public void ResourcePhases_SurviveALoadDrainedOnlyAtTheEnd()
        {
            using var scope = new MainThreadSequencerScope();
            using CoreViewModelFixture.SubscribedHarness harness = CoreViewModelFixture.CreateWithSubscriptions();

            // A busy user interface thread, which is the normal case: the load runs on a
            // worker and races through all four writes before anything is delivered, so
            // every queue is holding both an empty snapshot and a real one when the
            // drain finally happens. This is the arrangement that lost the phases.
            WriteLoadSequence(harness.Core, drainAfterEachWrite: null);

            scope.Pump.PendingCount.ShouldBeGreaterThan(
                0,
                "the deliveries must still be queued, otherwise this test is not exercising deferral at all");

            scope.Pump.Drain();

            AssertPhasesIntact(harness);
        }

        [Fact]
        public async Task LoadedPhases_SurviveAnEditThatRepublishesTheResourceSettings()
        {
            using var scope = new MainThreadSequencerScope();
            using CoreViewModelFixture.SubscribedHarness harness = CoreViewModelFixture.CreateWithSubscriptions();

            ProjectScenarioModel scenario = await CoreViewModelFixture.LoadProjectScenarioAsync(@"sample_v0_6_1.zpp");

            harness.Core.ProcessProjectScenario(scenario, Guid.NewGuid(), @"Test");
            scope.Pump.Drain();

            harness.Core.HasCompilationErrors.ShouldBeFalse();

            // The two resources in the sample that carry a phase. Reading them from the
            // scenario rather than hard coding keeps the test honest if the file changes.
            List<int> resourceIdsWithPhases =
                [.. scenario.ResourceSettings.Resources
                    .Where(x => x.InterActivityPhases.Count > 0)
                    .Select(x => x.Id)
                    .Order()];

            resourceIdsWithPhases.ShouldNotBeEmpty(
                "the sample file must still carry resources with inter-activity phases for this test to mean anything");

            Dictionary<int, IManagedResourceViewModel> managedResources =
                harness.ResourceSettingsManager.RawResources.ToDictionary(x => x.Id);

            foreach (int resourceId in resourceIdsWithPhases)
            {
                List<int> expected = scenario.ResourceSettings.Resources.Single(x => x.Id == resourceId).InterActivityPhases;

                managedResources[resourceId].InterActivityPhases.ShouldBe(
                    expected,
                    ignoreOrder: true,
                    $@"resource {resourceId} lost its phases while loading");
            }

            // Now the half that reaches the file. Any edit - a grid cell commit, a work
            // stream rename - ends up here, rebuilding the resource settings from these
            // view models and pushing them into the core, so a wipe that only damaged the
            // view models becomes a wipe of the saved plan.
            harness.ResourceSettingsManager.AreSettingsUpdated = true;
            scope.Pump.Drain();

            ProjectScenarioModel saved = harness.Core.BuildProjectScenario();

            foreach (int resourceId in resourceIdsWithPhases)
            {
                List<int> expected = scenario.ResourceSettings.Resources.Single(x => x.Id == resourceId).InterActivityPhases;

                saved.ResourceSettings.Resources.Single(x => x.Id == resourceId).InterActivityPhases.ShouldBe(
                    expected,
                    ignoreOrder: true,
                    $@"resource {resourceId} lost its phases on the way back into the project");
            }
        }

        /// <summary>
        /// Reproduces the settings writes a scenario load makes, in the order
        /// ClearSettings and ProcessProjectScenario make them: the reset clears the
        /// resource settings and then the work stream settings, and the scenario then
        /// supplies the work stream settings and then the resource settings. That
        /// ordering is the whole point - it is why the resource queue's drain is
        /// scheduled first - so it is spelled out here rather than borrowed from a
        /// scenario load that might quietly reorder it.
        /// </summary>
        private static void WriteLoadSequence(
            ICoreViewModel core,
            Action? drainAfterEachWrite)
        {
            var workStreamSettings = new WorkStreamSettingsModel
            {
                WorkStreams =
                [
                    new WorkStreamModel
                    {
                        Id = c_PhaseWorkStreamId,
                        Name = @"Development",
                        IsPhase = true,
                        DisplayOrder = 0,
                    },
                ],
            };

            var resourceSettings = new ResourceSettingsModel
            {
                Resources =
                [
                    new ResourceModel
                    {
                        Id = c_ResourceId,
                        Name = @"QA",
                        DisplayOrder = 0,
                        InterActivityAllocationType = InterActivityAllocationType.Indirect,
                        InterActivityPhases = [c_PhaseWorkStreamId],
                    },
                ],
            };

            // The reset.
            core.ResourceSettings = new ResourceSettingsModel();
            drainAfterEachWrite?.Invoke();
            core.WorkStreamSettings = new WorkStreamSettingsModel();
            drainAfterEachWrite?.Invoke();

            // The scenario.
            core.WorkStreamSettings = workStreamSettings;
            drainAfterEachWrite?.Invoke();
            core.ResourceSettings = resourceSettings;
            drainAfterEachWrite?.Invoke();
        }

        private static void AssertPhasesIntact(CoreViewModelFixture.SubscribedHarness harness)
        {
            IManagedResourceViewModel resource = harness.ResourceSettingsManager.RawResources.ShouldHaveSingleItem();

            resource.Id.ShouldBe(c_ResourceId);
            resource.InterActivityPhases.ShouldBe([c_PhaseWorkStreamId]);

            // The selector is what the dropdown binds to, and it is also where the phases
            // were actually destroyed: the resource derives its phase set from the
            // selector's selection, so a selector rebuilt against no work streams took
            // the phases with it. Asserting both catches a fix that repairs the set but
            // leaves the control showing nothing selected.
            resource.WorkStreamSelector.SelectedWorkStreamIds.ShouldBe([c_PhaseWorkStreamId]);
            resource.WorkStreamSelector.TargetWorkStreams.Select(x => x.Id).ShouldBe([c_PhaseWorkStreamId]);
        }
    }
}
