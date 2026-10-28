using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for the functions of ProjectScenarioHelper that make a scenario or a part of one: the empty scenario, the
    /// settings that an import gives to a plan, and the sort of the dependencies. The tests of the functions that change
    /// ids are in ProjectScenarioHelperTests and ProjectScenarioHelperEdgeCaseTests.
    /// </summary>
    /// <remarks>
    /// The functions take what they read as arguments, and so no core is needed. CoreViewModelScenarioTests holds the tests
    /// of what the core does with them.
    /// </remarks>
    public class ProjectScenarioHelperScenarioTests
    {
        private static readonly DateTimeOffset s_Today = new(2026, 9, 28, 0, 0, 0, TimeSpan.FromMinutes(330));

        #region CreateEmptyProjectScenario

        [Fact]
        public void CreateEmptyProjectScenario_Given_ADay_Then_TheScenarioStartsOnItAndHoldsNoActivities()
        {
            ProjectScenarioModel scenario = ProjectScenarioHelper.CreateEmptyProjectScenario(s_Today, new CoreViewModelFixture.TestSettingService());

            scenario.ProjectStart.ShouldBe(s_Today);
            scenario.ProjectStart.Offset.ShouldBe(s_Today.Offset);
            scenario.Today.ShouldBe(s_Today);
            scenario.Today.Offset.ShouldBe(s_Today.Offset);
            scenario.DependentActivities.ShouldBeEmpty();
            scenario.ResourceMetrics.ShouldBeEmpty();
            Json(scenario.Metrics).ShouldBe(Json(new MetricsModel()));
        }

        [Fact]
        public void CreateEmptyProjectScenario_Given_TheDefaults_Then_TheScenarioHoldsThem()
        {
            var settings = new CoreViewModelFixture.TestSettingService();

            ProjectScenarioModel scenario = ProjectScenarioHelper.CreateEmptyProjectScenario(s_Today, settings);

            Json(scenario.GraphSettings).ShouldBe(Json(settings.DefaultGraphSettings));
            Json(scenario.ResourceSettings).ShouldBe(Json(settings.DefaultResourceSettings));
            Json(scenario.WorkStreamSettings).ShouldBe(Json(settings.DefaultWorkStreamSettings));
            Json(scenario.HolidaySettings).ShouldBe(Json(settings.DefaultHolidaySettings));
            scenario.GraphSettings.ActivitySeverities.ShouldNotBeEmpty();
            scenario.ResourceSettings.DefaultUnitCost.ShouldBe(1.0);
            scenario.HolidaySettings.Holidays.Select(x => x.Id).ShouldBe(new[] { 1, 2, 3 });
        }

        [Fact]
        public void CreateEmptyProjectScenario_Given_TheDefaultsOfTheDisplay_Then_TheDisplaySettingsHoldThem()
        {
            // Each of the five has a value that differs from the one beside it, so that a swap shows.
            var settings = new CoreViewModelFixture.TestSettingService
            {
                DefaultShowDates = true,
                DefaultUseClassicDates = false,
                DefaultNonWorkingDayMode = NonWorkingDayMode.CustomCalendar,
                DefaultHideCost = true,
                DefaultHideBilling = false,
            };

            ProjectScenarioDisplaySettingsModel display = ProjectScenarioHelper.CreateEmptyProjectScenario(s_Today, settings).DisplaySettings;

            display.ShowDates.ShouldBeTrue();
            display.UseClassicDates.ShouldBeFalse();
            display.NonWorkingDayMode.ShouldBe(NonWorkingDayMode.CustomCalendar);
            display.HideCost.ShouldBeTrue();
            display.HideBilling.ShouldBeFalse();

            settings.DefaultShowDates = false;
            settings.DefaultUseClassicDates = true;
            settings.DefaultNonWorkingDayMode = NonWorkingDayMode.Weekends;
            settings.DefaultHideCost = false;
            settings.DefaultHideBilling = true;

            display = ProjectScenarioHelper.CreateEmptyProjectScenario(s_Today, settings).DisplaySettings;

            display.ShowDates.ShouldBeFalse();
            display.UseClassicDates.ShouldBeTrue();
            display.NonWorkingDayMode.ShouldBe(NonWorkingDayMode.Weekends);
            display.HideCost.ShouldBeFalse();
            display.HideBilling.ShouldBeTrue();
        }

        [Fact]
        public void CreateEmptyProjectScenario_Given_NoSettingService_Then_ItThrows()
        {
            Should.Throw<ArgumentNullException>(() => ProjectScenarioHelper.CreateEmptyProjectScenario(s_Today, null!))
                .ParamName.ShouldBe(@"settingService");
        }

        #endregion

        #region CreateImportedHolidaySettings

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void CreateImportedHolidaySettings_Given_Holidays_Then_TheyTakeThePlaceOfTheDefaultOnes(int count)
        {
            HolidaySettingsModel defaults = new CoreViewModelFixture.TestSettingService().DefaultHolidaySettings;
            ProjectScenarioImportModel import = Import(holidays:
                [.. Enumerable.Range(11, count).Select(id => new HolidayModel { Id = id, Name = $@"Holiday {id}" })]);

            HolidaySettingsModel settings = ProjectScenarioHelper.CreateImportedHolidaySettings(defaults, import);

            settings.Holidays.ShouldBe(import.Holidays);
            settings.Holidays.Count.ShouldBe(count);
        }

        [Fact]
        public void CreateImportedHolidaySettings_Given_NoHolidays_Then_TheDefaultsStay()
        {
            HolidaySettingsModel defaults = new CoreViewModelFixture.TestSettingService().DefaultHolidaySettings;

            HolidaySettingsModel settings = ProjectScenarioHelper.CreateImportedHolidaySettings(defaults, Import());

            Json(settings).ShouldBe(Json(defaults));
            settings.Holidays.Count.ShouldBe(3);
            settings.ShouldNotBeSameAs(defaults);
        }

        [Fact]
        public void CreateImportedHolidaySettings_Given_Holidays_Then_TheDefaultSettingsThatItIsGivenDoNotChange()
        {
            HolidaySettingsModel defaults = new CoreViewModelFixture.TestSettingService().DefaultHolidaySettings;

            ProjectScenarioHelper.CreateImportedHolidaySettings(defaults, Import(holidays: [new HolidayModel { Id = 11 }]));

            defaults.Holidays.Select(x => x.Id).ShouldBe(new[] { 1, 2, 3 });
        }

        [Fact]
        public void CreateImportedHolidaySettings_Given_NoArguments_Then_ItThrows()
        {
            Should.Throw<ArgumentNullException>(() => ProjectScenarioHelper.CreateImportedHolidaySettings(null!, Import()))
                .ParamName.ShouldBe(@"defaultHolidaySettings");
            Should.Throw<ArgumentNullException>(() => ProjectScenarioHelper.CreateImportedHolidaySettings(new HolidaySettingsModel(), null!))
                .ParamName.ShouldBe(@"projectScenarioImportModel");
        }

        #endregion

        #region CreateImportedWorkStreamSettings

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void CreateImportedWorkStreamSettings_Given_WorkStreams_Then_TheyTakeThePlaceOfTheDefaultOnes(int count)
        {
            WorkStreamSettingsModel defaults = new() { WorkStreams = [new WorkStreamModel { Id = 1, Name = @"Default" }] };
            ProjectScenarioImportModel import = Import(workStreams:
                [.. Enumerable.Range(5, count).Select(id => new WorkStreamModel { Id = id, Name = $@"Work stream {id}" })]);

            WorkStreamSettingsModel settings = ProjectScenarioHelper.CreateImportedWorkStreamSettings(defaults, import);

            settings.WorkStreams.ShouldBe(import.WorkStreams);
            settings.WorkStreams.Count.ShouldBe(count);
        }

        [Fact]
        public void CreateImportedWorkStreamSettings_Given_NoWorkStreams_Then_TheDefaultsStay()
        {
            WorkStreamSettingsModel defaults = new() { WorkStreams = [new WorkStreamModel { Id = 1, Name = @"Default" }] };

            WorkStreamSettingsModel settings = ProjectScenarioHelper.CreateImportedWorkStreamSettings(defaults, Import());

            Json(settings).ShouldBe(Json(defaults));
            settings.WorkStreams.ShouldHaveSingleItem().Name.ShouldBe(@"Default");
            settings.ShouldNotBeSameAs(defaults);
        }

        [Fact]
        public void CreateImportedWorkStreamSettings_Given_WorkStreams_Then_TheDefaultSettingsThatItIsGivenDoNotChange()
        {
            WorkStreamSettingsModel defaults = new() { WorkStreams = [new WorkStreamModel { Id = 1, Name = @"Default" }] };

            ProjectScenarioHelper.CreateImportedWorkStreamSettings(defaults, Import(workStreams: [new WorkStreamModel { Id = 5 }]));

            defaults.WorkStreams.ShouldHaveSingleItem().Id.ShouldBe(1);
        }

        [Fact]
        public void CreateImportedWorkStreamSettings_Given_NoArguments_Then_ItThrows()
        {
            Should.Throw<ArgumentNullException>(() => ProjectScenarioHelper.CreateImportedWorkStreamSettings(null!, Import()))
                .ParamName.ShouldBe(@"defaultWorkStreamSettings");
            Should.Throw<ArgumentNullException>(() => ProjectScenarioHelper.CreateImportedWorkStreamSettings(new WorkStreamSettingsModel(), null!))
                .ParamName.ShouldBe(@"projectScenarioImportModel");
        }

        #endregion

        #region CreateImportedResourceSettings

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void CreateImportedResourceSettings_Given_Resources_Then_TheyTakeThePlaceOfTheDefaultOnes(int count)
        {
            ResourceSettingsModel defaults = new() { Resources = [new ResourceModel { Id = 1, Name = @"Default" }] };
            ProjectScenarioImportModel import = Import(resourceSettings: new ResourceSettingsModel
            {
                Resources = [.. Enumerable.Range(5, count).Select(id => new ResourceModel { Id = id, Name = $@"Resource {id}" })],
            });

            ResourceSettingsModel settings = ProjectScenarioHelper.CreateImportedResourceSettings(defaults, import);

            settings.Resources.ShouldBe(import.ResourceSettings.Resources);
            settings.Resources.Count.ShouldBe(count);
        }

        [Fact]
        public void CreateImportedResourceSettings_Given_NoResources_Then_TheDefaultResourcesStay()
        {
            ResourceSettingsModel defaults = new() { Resources = [new ResourceModel { Id = 1, Name = @"Default" }] };

            ResourceSettingsModel settings = ProjectScenarioHelper.CreateImportedResourceSettings(defaults, Import());

            settings.Resources.ShouldHaveSingleItem().Name.ShouldBe(@"Default");
            settings.ShouldNotBeSameAs(defaults);
        }

        [Fact]
        public void CreateImportedResourceSettings_Given_Costs_Then_TheyAreTheImportsWhetherOrNotItHoldsResources()
        {
            ResourceSettingsModel defaults = new()
            {
                Resources = [new ResourceModel { Id = 1 }],
                DefaultUnitCost = 1.0,
                DefaultUnitBilling = 2.0,
                AreDisabled = false,
            };

            // A value that differs from the one beside it, so that a swap shows.
            var costs = new ResourceSettingsModel { DefaultUnitCost = 7.5, DefaultUnitBilling = 9.25, AreDisabled = true };

            foreach (ResourceSettingsModel imported in new[] { costs, costs with { Resources = [new ResourceModel { Id = 5 }] } })
            {
                ResourceSettingsModel settings = ProjectScenarioHelper.CreateImportedResourceSettings(defaults, Import(resourceSettings: imported));

                settings.DefaultUnitCost.ShouldBe(7.5);
                settings.DefaultUnitBilling.ShouldBe(9.25);
                settings.AreDisabled.ShouldBeTrue();
            }
        }

        [Fact]
        public void CreateImportedResourceSettings_Given_Resources_Then_TheDefaultSettingsThatItIsGivenDoNotChange()
        {
            ResourceSettingsModel defaults = new() { Resources = [new ResourceModel { Id = 1 }], DefaultUnitCost = 1.0 };

            ProjectScenarioHelper.CreateImportedResourceSettings(
                defaults,
                Import(resourceSettings: new ResourceSettingsModel { Resources = [new ResourceModel { Id = 5 }], DefaultUnitCost = 7.5 }));

            defaults.Resources.ShouldHaveSingleItem().Id.ShouldBe(1);
            defaults.DefaultUnitCost.ShouldBe(1.0);
        }

        [Fact]
        public void CreateImportedResourceSettings_Given_NoArguments_Then_ItThrows()
        {
            Should.Throw<ArgumentNullException>(() => ProjectScenarioHelper.CreateImportedResourceSettings(null!, Import()))
                .ParamName.ShouldBe(@"defaultResourceSettings");
            Should.Throw<ArgumentNullException>(() => ProjectScenarioHelper.CreateImportedResourceSettings(new ResourceSettingsModel(), null!))
                .ParamName.ShouldBe(@"projectScenarioImportModel");
        }

        #endregion

        #region CreateImportedGraphSettings

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void CreateImportedGraphSettings_Given_ActivitySeverities_Then_TheyTakeThePlaceOfTheDefaultOnesAndTheFormatsStay(int count)
        {
            GraphSettingsModel defaults = new CoreViewModelFixture.TestSettingService().DefaultGraphSettings;
            ProjectScenarioImportModel import = Import(activitySeverities:
                [.. Enumerable.Range(1, count).Select(index => new ActivitySeverityModel { SlackLimit = index * 3, CriticalityWeight = 5.0 - index })]);

            GraphSettingsModel settings = ProjectScenarioHelper.CreateImportedGraphSettings(defaults, import);

            settings.ActivitySeverities.ShouldBe(import.ActivitySeverities);
            settings.ActivitySeverities.Count.ShouldBe(count);
            Json(settings.NodeTypeFormats).ShouldBe(Json(defaults.NodeTypeFormats));
            Json(settings.EdgeTypeFormats).ShouldBe(Json(defaults.EdgeTypeFormats));
            settings.NodeTypeFormats.ShouldNotBeEmpty();
            settings.EdgeTypeFormats.ShouldNotBeEmpty();
        }

        [Fact]
        public void CreateImportedGraphSettings_Given_NoActivitySeverities_Then_TheDefaultsStay()
        {
            GraphSettingsModel defaults = new CoreViewModelFixture.TestSettingService().DefaultGraphSettings;

            GraphSettingsModel settings = ProjectScenarioHelper.CreateImportedGraphSettings(defaults, Import());

            Json(settings).ShouldBe(Json(defaults));
            settings.ActivitySeverities.Count.ShouldBe(4);
            settings.ShouldNotBeSameAs(defaults);
        }

        [Fact]
        public void CreateImportedGraphSettings_Given_ActivitySeverities_Then_TheDefaultSettingsThatItIsGivenDoNotChange()
        {
            GraphSettingsModel defaults = new CoreViewModelFixture.TestSettingService().DefaultGraphSettings;

            ProjectScenarioHelper.CreateImportedGraphSettings(defaults, Import(activitySeverities: [new ActivitySeverityModel { SlackLimit = 3 }]));

            defaults.ActivitySeverities.Count.ShouldBe(4);
        }

        [Fact]
        public void CreateImportedGraphSettings_Given_NoArguments_Then_ItThrows()
        {
            Should.Throw<ArgumentNullException>(() => ProjectScenarioHelper.CreateImportedGraphSettings(null!, Import()))
                .ParamName.ShouldBe(@"defaultGraphSettings");
            Should.Throw<ArgumentNullException>(() => ProjectScenarioHelper.CreateImportedGraphSettings(new GraphSettingsModel(), null!))
                .ParamName.ShouldBe(@"projectScenarioImportModel");
        }

        #endregion

        #region SortDependencies

        [Fact]
        public void SortDependencies_Given_ListsInAnyOrder_Then_EachOfTheFourIsInOrder()
        {
            List<DependentActivityModel> activities =
            [
                Activity(1, dependencies: [3, 1, 2], planningDependencies: [9, 7, 8], resourceDependencies: [6, 4, 5], successors: [12, 10, 11]),
                Activity(2, dependencies: [5, 4], planningDependencies: [], resourceDependencies: [], successors: [20, 19]),
            ];

            ProjectScenarioHelper.SortDependencies(activities);

            activities[0].Dependencies.ShouldBe(new[] { 1, 2, 3 });
            activities[0].PlanningDependencies.ShouldBe(new[] { 7, 8, 9 });
            activities[0].ResourceDependencies.ShouldBe(new[] { 4, 5, 6 });
            activities[0].Successors.ShouldBe(new[] { 10, 11, 12 });
            activities[1].Dependencies.ShouldBe(new[] { 4, 5 });
            activities[1].PlanningDependencies.ShouldBeEmpty();
            activities[1].ResourceDependencies.ShouldBeEmpty();
            activities[1].Successors.ShouldBe(new[] { 19, 20 });
        }

        [Fact]
        public void SortDependencies_Given_ListsThatAreInOrder_Then_TheyDoNotChange()
        {
            DependentActivityModel activity = Activity(1, dependencies: [1, 2, 2, 3], planningDependencies: [4], resourceDependencies: [5, 6], successors: [7, 8]);

            ProjectScenarioHelper.SortDependencies([activity]);

            activity.Dependencies.ShouldBe(new[] { 1, 2, 2, 3 });
            activity.PlanningDependencies.ShouldBe(new[] { 4 });
            activity.ResourceDependencies.ShouldBe(new[] { 5, 6 });
            activity.Successors.ShouldBe(new[] { 7, 8 });
        }

        [Fact]
        public void SortDependencies_Given_Activities_Then_OnlyTheOrderOfTheListsChanges()
        {
            DependentActivityModel activity = Activity(7, dependencies: [3, 1, 2], planningDependencies: [9, 8], resourceDependencies: [6, 5], successors: [12, 11]);
            ActivityModel before = activity.Activity;

            ProjectScenarioHelper.SortDependencies([activity]);

            activity.Activity.ShouldBeSameAs(before);
            activity.Dependencies.Count.ShouldBe(3);
            activity.PlanningDependencies.Count.ShouldBe(2);
            activity.ResourceDependencies.Count.ShouldBe(2);
            activity.Successors.Count.ShouldBe(2);
        }

        [Fact]
        public void SortDependencies_Given_NoActivities_Then_NothingHappens()
        {
            Should.NotThrow(() => ProjectScenarioHelper.SortDependencies([]));
        }

        [Fact]
        public void SortDependencies_Given_NoSequence_Then_ItThrows()
        {
            Should.Throw<ArgumentNullException>(() => ProjectScenarioHelper.SortDependencies(null!))
                .ParamName.ShouldBe(@"dependentActivities");
        }

        #endregion

        private static string Json<T>(T value) => JsonSerializer.Serialize(value);

        private static ProjectScenarioImportModel Import(
            List<HolidayModel>? holidays = null,
            List<WorkStreamModel>? workStreams = null,
            ResourceSettingsModel? resourceSettings = null,
            List<ActivitySeverityModel>? activitySeverities = null) =>
            new()
            {
                Holidays = holidays ?? [],
                WorkStreams = workStreams ?? [],
                ResourceSettings = resourceSettings ?? new ResourceSettingsModel(),
                ActivitySeverities = activitySeverities ?? [],
            };

        private static DependentActivityModel Activity(
            int id,
            List<int> dependencies,
            List<int> planningDependencies,
            List<int> resourceDependencies,
            List<int> successors) =>
            new()
            {
                Activity = new ActivityModel { Id = id, Name = $@"Activity {id}" },
                Dependencies = dependencies,
                PlanningDependencies = planningDependencies,
                ResourceDependencies = resourceDependencies,
                Successors = successors,
            };
    }
}
