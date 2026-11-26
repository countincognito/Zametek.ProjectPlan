using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The switches that hide parts of the plan from the Gantt chart and the resource chart: IsHiddenFromCharts of an
    /// activity, a resource and a work stream, and the two switches that show the hidden parts again. A scenario keeps them
    /// through a load and a save.
    /// </summary>
    public class CoreViewModelHiddenFromChartsTests
    {
        [Fact]
        public void BuildProjectScenario_Given_AScenarioWithEachSwitch_Then_TheSavedScenarioHasEachSwitchAsItWasLoaded()
        {
            using CoreViewModel core = Load(PlanWithSwitches());

            ProjectScenarioModel saved = core.BuildProjectScenario();

            saved.DependentActivities.OrderBy(x => x.Activity.Id).Select(x => x.Activity.IsHiddenFromCharts)
                .ShouldBe([false, true, false]);
            saved.ResourceSettings.Resources.OrderBy(x => x.Id).Select(x => x.IsHiddenFromCharts)
                .ShouldBe([true, false]);
            saved.WorkStreamSettings.WorkStreams.OrderBy(x => x.Id).Select(x => x.IsHiddenFromCharts)
                .ShouldBe([false, true]);
            saved.DisplaySettings.GanttChartShowHidden.ShouldBeTrue();
            saved.DisplaySettings.ResourceChartShowHidden.ShouldBeFalse();
        }

        [Fact]
        public void BuildProjectScenario_Given_AScenarioWithNoSwitch_Then_NothingIsHidden()
        {
            using CoreViewModel core = Load(CoreViewModelFixture.CreateProjectScenario(3));

            ProjectScenarioModel saved = core.BuildProjectScenario();

            saved.DependentActivities.ShouldAllBe(x => !x.Activity.IsHiddenFromCharts);
            saved.ResourceSettings.Resources.ShouldAllBe(x => !x.IsHiddenFromCharts);
            saved.DisplaySettings.GanttChartShowHidden.ShouldBeFalse();
            saved.DisplaySettings.ResourceChartShowHidden.ShouldBeFalse();
        }

        [Fact]
        public async Task OpenProjectFile_Given_AFileOfVersion0_6_1_Then_NothingIsHiddenFromTheCharts()
        {
            ProjectScenarioModel scenario = await CoreViewModelFixture.LoadProjectScenarioAsync(@"sample_v0_6_1.zpp");

            scenario.DependentActivities.ShouldNotBeEmpty();
            scenario.DependentActivities.ShouldAllBe(x => !x.Activity.IsHiddenFromCharts);
            scenario.ResourceSettings.Resources.ShouldNotBeEmpty();
            scenario.ResourceSettings.Resources.ShouldAllBe(x => !x.IsHiddenFromCharts);
            scenario.WorkStreamSettings.WorkStreams.ShouldNotBeEmpty();
            scenario.WorkStreamSettings.WorkStreams.ShouldAllBe(x => !x.IsHiddenFromCharts);
            scenario.DisplaySettings.GanttChartShowHidden.ShouldBeFalse();
            scenario.DisplaySettings.ResourceChartShowHidden.ShouldBeFalse();
        }

        [Fact]
        public void IsHiddenFromCharts_Given_AnActivityThatTheUserSwitches_Then_TheScenarioIsChangedAndNothingIsStaleOrCompiled()
        {
            using CoreViewModel core = LoadSaved(PlanWithSwitches());
            int revision = core.ChartVisibilityRevision;
            IManagedActivityViewModel activity = core.RawActivities.Single(x => x.Id == 1);

            activity.IsHiddenFromCharts = true;

            activity.IsHiddenFromCharts.ShouldBeTrue();
            core.IsProjectScenarioUpdated.ShouldBeTrue();
            core.HasStaleOutputs.ShouldBeFalse();
            activity.IsCompiled.ShouldBeTrue("an edit marks the activity as not compiled, and this is not an edit");
            core.ChartVisibilityRevision.ShouldBe(revision + 1);
            core.BuildProjectScenario().DependentActivities.Single(x => x.Activity.Id == 1).Activity.IsHiddenFromCharts.ShouldBeTrue();
        }

        [Fact]
        public void IsHiddenFromCharts_Given_AMutedActivity_Then_TheCoreIsNotTold()
        {
            // The dialog of the activities mutes the activity, and the core then tells the charts itself.
            using CoreViewModel core = LoadSaved(PlanWithSwitches());
            int revision = core.ChartVisibilityRevision;
            IManagedActivityViewModel activity = core.RawActivities.Single(x => x.Id == 1);

            activity.IsEditMuted = true;
            activity.IsHiddenFromCharts = true;
            activity.IsEditMuted = false;

            core.IsProjectScenarioUpdated.ShouldBeFalse();
            core.ChartVisibilityRevision.ShouldBe(revision);
        }

        [Fact]
        public void UpdateManagedActivities_Given_OnlyTheSwitch_Then_ItIsSetAndNothingIsStaleOrCompiled()
        {
            using CoreViewModel core = LoadSaved(PlanWithSwitches());
            int revision = core.ChartVisibilityRevision;

            core.UpdateManagedActivities(
            [
                new UpdateDependentActivityModel { Id = 1, IsHiddenFromCharts = true, IsIsHiddenFromChartsEdited = true },
                new UpdateDependentActivityModel { Id = 2, IsHiddenFromCharts = false, IsIsHiddenFromChartsEdited = true },
            ]);

            core.RawActivities.OrderBy(x => x.Id).Select(x => x.IsHiddenFromCharts).ShouldBe([true, false, false]);
            core.IsProjectScenarioUpdated.ShouldBeTrue();
            core.HasStaleOutputs.ShouldBeFalse();
            core.RawActivities.ShouldAllBe(x => x.IsCompiled);
            core.ChartVisibilityRevision.ShouldBe(revision + 1);
        }

        [Fact]
        public void UpdateManagedActivities_Given_TheSwitchAndAnotherEdit_Then_BothAreSetAndTheOutputsAreStale()
        {
            using CoreViewModel core = LoadSaved(PlanWithSwitches());
            core.AutoCompile = false;
            int revision = core.ChartVisibilityRevision;

            core.UpdateManagedActivities(
            [
                new UpdateDependentActivityModel { Id = 1, IsHiddenFromCharts = true, IsIsHiddenFromChartsEdited = true, HasNoCost = true, IsHasNoCostEdited = true },
            ]);

            IManagedActivityViewModel activity = core.RawActivities.Single(x => x.Id == 1);
            activity.IsHiddenFromCharts.ShouldBeTrue();
            activity.HasNoCost.ShouldBeTrue();
            core.HasStaleOutputs.ShouldBeTrue("the cost is an input of a compile");
            core.ChartVisibilityRevision.ShouldBe(revision + 1);
        }

        [Fact]
        public void SetResourceChartVisibility_Given_ASwitchThatChanges_Then_OnlyThatSwitchChangesAndNothingIsStale()
        {
            using CoreViewModel core = LoadSaved(PlanWithSwitches());
            ResourceSettingsModel before = core.ResourceSettings;
            int revision = core.ChartVisibilityRevision;
            List<string> raised = [];
            core.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? string.Empty);

            ResourceSettingsModel after = core.SetResourceChartVisibility(new Dictionary<int, bool> { [1] = false, [2] = true });

            after.ShouldBeSameAs(core.ResourceSettings);
            after.Resources.Select(x => x.IsHiddenFromCharts).ShouldBe([false, true]);
            // With the switch as it was, each resource is the one that it was.
            after.Resources.Zip(before.Resources, (a, b) => a with { IsHiddenFromCharts = b.IsHiddenFromCharts } == b).ShouldAllBe(x => x);
            after.DefaultUnitCost.ShouldBe(before.DefaultUnitCost);
            core.IsProjectScenarioUpdated.ShouldBeTrue();
            core.HasStaleOutputs.ShouldBeFalse();
            core.ChartVisibilityRevision.ShouldBe(revision + 1);
            raised.ShouldContain(nameof(ICoreViewModel.ResourceSettings));
        }

        [Fact]
        public void SetResourceChartVisibility_Given_TheSwitchesThatTheResourcesHave_Then_NothingChanges()
        {
            using CoreViewModel core = LoadSaved(PlanWithSwitches());
            ResourceSettingsModel before = core.ResourceSettings;
            int revision = core.ChartVisibilityRevision;

            core.SetResourceChartVisibility(new Dictionary<int, bool> { [1] = true, [2] = false }).ShouldBeSameAs(before);

            core.IsProjectScenarioUpdated.ShouldBeFalse();
            core.ChartVisibilityRevision.ShouldBe(revision);
        }

        [Fact]
        public void SetWorkStreamChartVisibility_Given_ASwitchThatChanges_Then_OnlyThatSwitchChangesAndNothingIsStale()
        {
            using CoreViewModel core = LoadSaved(PlanWithSwitches());
            int revision = core.ChartVisibilityRevision;

            WorkStreamSettingsModel after = core.SetWorkStreamChartVisibility(new Dictionary<int, bool> { [1] = true });

            after.ShouldBeSameAs(core.WorkStreamSettings);
            after.WorkStreams.Select(x => x.IsHiddenFromCharts).ShouldBe([true, true]);
            after.WorkStreams.Select(x => x.Name).ShouldBe([@"AA", @"BB"]);
            core.IsProjectScenarioUpdated.ShouldBeTrue();
            core.HasStaleOutputs.ShouldBeFalse();
            core.ChartVisibilityRevision.ShouldBe(revision + 1);
        }

        [Fact]
        public void ResourceSettings_Given_TheSwitchAndAnotherField_Then_TheChartsBuildAgainAndTheOutputsAreStale()
        {
            // As the dialog of the resources pushes an edit of the switch and of the unit cost.
            using CoreViewModel core = LoadSaved(PlanWithSwitches());
            int revision = core.ChartVisibilityRevision;

            core.ResourceSettings = core.ResourceSettings with
            {
                Resources = [.. core.ResourceSettings.Resources.Select(x => x.Id == 2 ? x with { IsHiddenFromCharts = true, UnitCost = 5.0 } : x)],
            };

            core.ChartVisibilityRevision.ShouldBe(revision + 1);
            core.HasStaleOutputs.ShouldBeTrue("the unit cost is an input of a compile");
        }

        [Fact]
        public void ResourceSettings_Given_TheSameHiddenResources_Then_TheCounterStays()
        {
            // An edit of another field only. Each part has new settings, but the same parts are hidden.
            using CoreViewModel core = LoadSaved(PlanWithSwitches());
            int revision = core.ChartVisibilityRevision;

            core.ResourceSettings = core.ResourceSettings with
            {
                Resources = [.. core.ResourceSettings.Resources.Select(x => x with { UnitCost = 5.0 })],
            };

            core.ChartVisibilityRevision.ShouldBe(revision);
            core.HasStaleOutputs.ShouldBeTrue();
        }

        [Fact]
        public void WorkStreamSettings_Given_TheRemovalOfAHiddenWorkStream_Then_TheChartsBuildAgain()
        {
            // As the grid of the work streams pushes the removal of work stream 2, which is hidden.
            using CoreViewModel core = LoadSaved(PlanWithSwitches());
            int revision = core.ChartVisibilityRevision;

            core.WorkStreamSettings = core.WorkStreamSettings with
            {
                WorkStreams = [.. core.WorkStreamSettings.WorkStreams.Where(x => x.Id != 2)],
            };

            core.ChartVisibilityRevision.ShouldBe(revision + 1);
        }

        [Fact]
        public void IsHiddenFromCharts_Given_AResourceAndAWorkStreamThatTheUserSwitches_Then_TheCoreHasThemAndTheGridsStay()
        {
            // Both pumps, before the harness starts its subscriptions: the outputs that the load builds come through the
            // thread pool, and their metrics mark the scenario. On the real thread pool, they came at any moment, also
            // after the check below (a hook of 2026-11-25 failed so).
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using CoreViewModelFixture.SubscribedHarness harness = CoreViewModelFixture.CreateWithSubscriptions();
            harness.Core.ProcessProjectScenario(PlanWithSwitches(), Guid.NewGuid(), @"Plan");
            SequencerScopes.Drain(main, pool);

            // As a save does.
            harness.Core.IsProjectScenarioUpdated = false;
            harness.Core.HasStaleOutputs.ShouldBeFalse("precondition: the load compiled the plan");
            IManagedResourceViewModel resource = harness.ResourceSettingsManager.RawResources.Single(x => x.Id == 2);
            IManagedWorkStreamViewModel workStream = harness.WorkStreamSettingsManager.RawWorkStreams.Single(x => x.Id == 1);

            resource.IsHiddenFromCharts = true;
            workStream.IsHiddenFromCharts = true;
            SequencerScopes.Drain(main, pool);

            harness.Core.ResourceSettings.Resources.Single(x => x.Id == 2).IsHiddenFromCharts.ShouldBeTrue();
            harness.Core.WorkStreamSettings.WorkStreams.Single(x => x.Id == 1).IsHiddenFromCharts.ShouldBeTrue();
            harness.Core.IsProjectScenarioUpdated.ShouldBeTrue();
            harness.Core.HasStaleOutputs.ShouldBeFalse();
            harness.ResourceSettingsManager.AreSettingsUpdated.ShouldBeFalse();
            harness.WorkStreamSettingsManager.AreSettingsUpdated.ShouldBeFalse();

            // The managers knew the settings as their own, and so they did not build their grids again.
            harness.ResourceSettingsManager.RawResources.Single(x => x.Id == 2).ShouldBeSameAs(resource);
            harness.WorkStreamSettingsManager.RawWorkStreams.Single(x => x.Id == 1).ShouldBeSameAs(workStream);
        }

        // Three activities with two resources and two work streams: activity 2, resource 1 and work stream 2 are hidden,
        // and the Gantt chart shows the hidden parts.
        private static ProjectScenarioModel PlanWithSwitches()
        {
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(3);
            return scenario with
            {
                DependentActivities =
                [
                    .. scenario.DependentActivities.Select(x => x with
                    {
                        Activity = x.Activity with { IsHiddenFromCharts = x.Activity.Id == 2, TargetWorkStreams = [1 + (x.Activity.Id % 2)] },
                    }),
                ],
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
                DisplaySettings = scenario.DisplaySettings with { GanttChartShowHidden = true },
            };
        }

        // A scenario as it is after an open: compiled, with no mark of a change and no stale output.
        private static CoreViewModel LoadSaved(ProjectScenarioModel plan)
        {
            CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(plan, Guid.NewGuid(), @"Plan");
            core.IsProjectScenarioUpdated.ShouldBeFalse("precondition: a load is not an edit");
            core.HasStaleOutputs.ShouldBeFalse("precondition: the load compiled the plan");
            return core;
        }

        private static CoreViewModel Load(ProjectScenarioModel plan)
        {
            CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(plan, Guid.NewGuid(), @"Plan");
            core.RunCompile();
            return core;
        }
    }
}
