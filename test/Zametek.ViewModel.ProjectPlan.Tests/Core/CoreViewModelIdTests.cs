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
    /// Tests for the three members of the core that give the activities, the resources or the work streams of the scenario
    /// other ids: each builds the scenario, changes the ids in it, and loads it again under the same id and the same title.
    /// </summary>
    /// <remarks>
    /// The user interface calls them, and no other code does. The tests go through the public members of the core, and
    /// they say what a renumbering does to the scenario, and not which code does it.
    /// </remarks>
    public class CoreViewModelIdTests
    {
        private static readonly Guid s_ScenarioId = new(@"8d0e2f3a-0c41-4a5e-9f55-2b1d3c4e5f60");

        #region Activities

        [Fact]
        public void UpdateManagedActivityIds_Given_AMapping_Then_TheActivityAndTheLinksToItFollow()
        {
            using Loaded loaded = Loaded.Make();

            loaded.Core.UpdateManagedActivityIds([(2, 5)]);

            ProjectScenarioModel scenario = loaded.Core.BuildProjectScenario();
            scenario.DependentActivities.Select(x => x.Activity.Id).Order().ShouldBe(new[] { 1, 3, 5 });
            scenario.DependentActivities.Single(x => x.Activity.Id == 5).Activity.Name.ShouldBe(@"Activity 2");
            scenario.DependentActivities.Single(x => x.Activity.Id == 5).Dependencies.ShouldBe(new[] { 1 });
            scenario.DependentActivities.Single(x => x.Activity.Id == 3).Dependencies.ShouldBe(new[] { 5 });
        }

        [Fact]
        public void UpdateManagedActivityIds_Given_AMapping_Then_OnlyTheActivitiesAreRenumbered()
        {
            using Loaded loaded = Loaded.Make();

            loaded.Core.UpdateManagedActivityIds([(2, 5)]);

            ProjectScenarioModel scenario = loaded.Core.BuildProjectScenario();
            scenario.ResourceSettings.Resources.Select(x => x.Id).Order().ShouldBe(new[] { 1, 2 });
            scenario.WorkStreamSettings.WorkStreams.Select(x => x.Id).Order().ShouldBe(new[] { 1, 2 });
        }

        [Fact]
        public void UpdateManagedActivityIds_Given_AMapping_Then_TheScenarioKeepsItsIdAndItsTitleAndIsMarkedUpdated()
        {
            using Loaded loaded = Loaded.Make();
            loaded.Core.IsProjectScenarioUpdated.ShouldBeFalse();

            loaded.Core.UpdateManagedActivityIds([(2, 5)]);

            loaded.Settings.ScenarioId.ShouldBe(s_ScenarioId);
            loaded.Settings.ScenarioTitle.ShouldBe(@"Renumbering");
            loaded.Core.IsProjectScenarioUpdated.ShouldBeTrue();
        }

        [Fact]
        public void UpdateManagedActivityIds_Given_AMapping_Then_TheBusySignalChangesOnceOnAndOnceOff()
        {
            using Loaded loaded = Loaded.Make();
            List<bool> busy = WatchBusy(loaded.Core);

            loaded.Core.UpdateManagedActivityIds([(2, 5)]);

            busy.ShouldBe(new[] { true, false });
        }

        #endregion

        #region Resources

        [Fact]
        public void UpdateManagedResourceIds_Given_AMapping_Then_TheResourceAndTheTargetsOfTheActivitiesFollow()
        {
            using Loaded loaded = Loaded.Make();

            loaded.Core.UpdateManagedResourceIds([(1, 4)]);

            ProjectScenarioModel scenario = loaded.Core.BuildProjectScenario();
            scenario.ResourceSettings.Resources.Select(x => x.Id).Order().ShouldBe(new[] { 2, 4 });
            scenario.ResourceSettings.Resources.Single(x => x.Id == 4).Name.ShouldBe(@"Resource 1");

            // Activity 2 targeted resource 1, and activities 1 and 3 targeted resource 2.
            scenario.DependentActivities.Single(x => x.Activity.Id == 2).Activity.TargetResources.ShouldBe(new[] { 4 });
            scenario.DependentActivities.Single(x => x.Activity.Id == 1).Activity.TargetResources.ShouldBe(new[] { 2 });
        }

        [Fact]
        public void UpdateManagedResourceIds_Given_AMapping_Then_OnlyTheResourcesAreRenumbered()
        {
            using Loaded loaded = Loaded.Make();

            loaded.Core.UpdateManagedResourceIds([(1, 4)]);

            ProjectScenarioModel scenario = loaded.Core.BuildProjectScenario();
            scenario.DependentActivities.Select(x => x.Activity.Id).Order().ShouldBe(new[] { 1, 2, 3 });
            scenario.WorkStreamSettings.WorkStreams.Select(x => x.Id).Order().ShouldBe(new[] { 1, 2 });
        }

        [Fact]
        public void UpdateManagedResourceIds_Given_AMapping_Then_TheScenarioKeepsItsIdAndItsTitleAndIsMarkedUpdated()
        {
            using Loaded loaded = Loaded.Make();
            loaded.Core.IsProjectScenarioUpdated.ShouldBeFalse();

            loaded.Core.UpdateManagedResourceIds([(1, 4)]);

            loaded.Settings.ScenarioId.ShouldBe(s_ScenarioId);
            loaded.Settings.ScenarioTitle.ShouldBe(@"Renumbering");
            loaded.Core.IsProjectScenarioUpdated.ShouldBeTrue();
        }

        [Fact]
        public void UpdateManagedResourceIds_Given_AMapping_Then_TheBusySignalChangesOnceOnAndOnceOff()
        {
            using Loaded loaded = Loaded.Make();
            List<bool> busy = WatchBusy(loaded.Core);

            loaded.Core.UpdateManagedResourceIds([(1, 4)]);

            busy.ShouldBe(new[] { true, false });
        }

        #endregion

        #region Work streams

        [Fact]
        public void UpdateManagedWorkStreamIds_Given_AMapping_Then_TheWorkStreamAndTheTargetsOfTheActivitiesFollow()
        {
            using Loaded loaded = Loaded.Make();

            loaded.Core.UpdateManagedWorkStreamIds([(1, 4)]);

            ProjectScenarioModel scenario = loaded.Core.BuildProjectScenario();
            scenario.WorkStreamSettings.WorkStreams.Select(x => x.Id).Order().ShouldBe(new[] { 2, 4 });
            scenario.WorkStreamSettings.WorkStreams.Single(x => x.Id == 4).Name.ShouldBe(@"Build");

            // Activity 2 targeted work stream 1, and activities 1 and 3 targeted work stream 2.
            scenario.DependentActivities.Single(x => x.Activity.Id == 2).Activity.TargetWorkStreams.ShouldBe(new[] { 4 });
            scenario.DependentActivities.Single(x => x.Activity.Id == 1).Activity.TargetWorkStreams.ShouldBe(new[] { 2 });
        }

        [Fact]
        public void UpdateManagedWorkStreamIds_Given_AMapping_Then_OnlyTheWorkStreamsAreRenumbered()
        {
            using Loaded loaded = Loaded.Make();

            loaded.Core.UpdateManagedWorkStreamIds([(1, 4)]);

            ProjectScenarioModel scenario = loaded.Core.BuildProjectScenario();
            scenario.DependentActivities.Select(x => x.Activity.Id).Order().ShouldBe(new[] { 1, 2, 3 });
            scenario.ResourceSettings.Resources.Select(x => x.Id).Order().ShouldBe(new[] { 1, 2 });
        }

        [Fact]
        public void UpdateManagedWorkStreamIds_Given_AMapping_Then_TheScenarioKeepsItsIdAndItsTitleAndIsMarkedUpdated()
        {
            using Loaded loaded = Loaded.Make();
            loaded.Core.IsProjectScenarioUpdated.ShouldBeFalse();

            loaded.Core.UpdateManagedWorkStreamIds([(1, 4)]);

            loaded.Settings.ScenarioId.ShouldBe(s_ScenarioId);
            loaded.Settings.ScenarioTitle.ShouldBe(@"Renumbering");
            loaded.Core.IsProjectScenarioUpdated.ShouldBeTrue();
        }

        [Fact]
        public void UpdateManagedWorkStreamIds_Given_AMapping_Then_TheBusySignalChangesOnceOnAndOnceOff()
        {
            using Loaded loaded = Loaded.Make();
            List<bool> busy = WatchBusy(loaded.Core);

            loaded.Core.UpdateManagedWorkStreamIds([(1, 4)]);

            busy.ShouldBe(new[] { true, false });
        }

        #endregion

        // The value of the busy signal each time that the core raises it.
        private static List<bool> WatchBusy(CoreViewModel core)
        {
            List<bool> busy = [];

            core.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ICoreViewModel.IsBusy))
                {
                    busy.Add(core.IsBusy);
                }
            };

            return busy;
        }

        // A core that holds the plan of three chained activities, with two resources and two work streams that the
        // activities target in turn, and that has loaded it under a known id and title.
        private sealed class Loaded
            : IDisposable
        {
            private Loaded(CoreViewModel core, ISettingService settings)
            {
                Core = core;
                Settings = settings;
            }

            public CoreViewModel Core { get; }

            public ISettingService Settings { get; }

            public static Loaded Make()
            {
                (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();

                ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(3);
                scenario = scenario with
                {
                    WorkStreamSettings = new WorkStreamSettingsModel
                    {
                        WorkStreams =
                        [
                            new WorkStreamModel { Id = 1, Name = @"Build", DisplayOrder = 1 },
                            new WorkStreamModel { Id = 2, Name = @"Test", DisplayOrder = 2 },
                        ],
                    },
                    DependentActivities =
                    [
                        .. scenario.DependentActivities.Select(x => x with
                        {
                            Activity = x.Activity with { TargetWorkStreams = [1 + (x.Activity.Id % 2)] },
                        }),
                    ],
                };

                core.ProcessProjectScenario(scenario, s_ScenarioId, @"Renumbering");

                return new Loaded(core, settings);
            }

            public void Dispose() => Core.Dispose();
        }
    }
}
