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
    /// Tests for the scenarios that the core makes: the empty scenario, the settings that an import gives to a plan, and the
    /// order of the dependencies in the scenario that the core builds from the plan.
    /// </summary>
    /// <remarks>
    /// The tests go through the public members of the core. They say what these scenarios hold, and not which class makes
    /// them, and so they hold when the code that makes them moves.
    /// </remarks>
    public class CoreViewModelScenarioTests
    {
        private static readonly DateTimeOffset s_Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        #region CreateEmptyProjectScenario

        [Fact]
        public void CreateEmptyProjectScenario_Then_ItHoldsNoActivitiesAndTheDefaultSettings()
        {
            var made = CoreViewModelFixture.CreateWithSettingService();
            using CoreViewModel core = made.CoreViewModel;
            ISettingService settings = made.SettingService;

            ProjectScenarioModel scenario = core.CreateEmptyProjectScenario();

            scenario.DependentActivities.ShouldBeEmpty();
            scenario.ResourceMetrics.ShouldBeEmpty();
            Json(scenario.Metrics).ShouldBe(Json(new MetricsModel()));
            scenario.ProjectStart.ShouldBe(scenario.Today);
            Json(scenario.GraphSettings).ShouldBe(Json(settings.DefaultGraphSettings));
            Json(scenario.ResourceSettings).ShouldBe(Json(settings.DefaultResourceSettings));
            Json(scenario.WorkStreamSettings).ShouldBe(Json(settings.DefaultWorkStreamSettings));
            Json(scenario.HolidaySettings).ShouldBe(Json(settings.DefaultHolidaySettings));
            scenario.GraphSettings.ActivitySeverities.ShouldNotBeEmpty();
            scenario.HolidaySettings.Holidays.Select(x => x.Id).ShouldBe(new[] { 1, 2, 3 });
        }

        [Fact]
        public void CreateEmptyProjectScenario_Given_TheDefaultsOfTheDisplay_Then_TheDisplaySettingsHoldThem()
        {
            var made = CoreViewModelFixture.CreateWithSettingService();
            using CoreViewModel core = made.CoreViewModel;
            ISettingService settings = made.SettingService;

            // Each of the five has a value that differs from the one beside it, so that a swap shows.
            settings.DefaultShowDates = true;
            settings.DefaultUseClassicDates = false;
            settings.DefaultNonWorkingDayMode = NonWorkingDayMode.CustomCalendar;
            settings.DefaultHideCost = true;
            settings.DefaultHideBilling = false;

            ProjectScenarioDisplaySettingsModel display = core.CreateEmptyProjectScenario().DisplaySettings;

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

            display = core.CreateEmptyProjectScenario().DisplaySettings;

            display.ShowDates.ShouldBeFalse();
            display.UseClassicDates.ShouldBeTrue();
            display.NonWorkingDayMode.ShouldBe(NonWorkingDayMode.Weekends);
            display.HideCost.ShouldBeFalse();
            display.HideBilling.ShouldBeTrue();
        }

        [Fact]
        public void CreateEmptyProjectScenario_Given_SettingsThatWereEdited_Then_ItStillHoldsTheDefaults()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.HolidaySettings = new HolidaySettingsModel();
            core.WorkStreamSettings = new WorkStreamSettingsModel { WorkStreams = [new WorkStreamModel { Id = 1, Name = @"Build" }] };
            core.ResourceSettings = new ResourceSettingsModel { DefaultUnitCost = 9.5 };

            ProjectScenarioModel scenario = core.CreateEmptyProjectScenario();

            scenario.HolidaySettings.Holidays.Select(x => x.Id).ShouldBe(new[] { 1, 2, 3 });
            scenario.WorkStreamSettings.WorkStreams.ShouldBeEmpty();
            scenario.ResourceSettings.DefaultUnitCost.ShouldBe(1.0);
        }

        [Fact]
        public void CreateEmptyProjectScenario_Then_TheBusySignalIsOnWhileItIsMadeAndOffAfter()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            List<bool> busy = WatchBusy(core);

            core.CreateEmptyProjectScenario();

            busy.ShouldBe(new[] { true, false });
            core.IsBusy.ShouldBeFalse();
        }

        #endregion

        #region ProcessProjectScenarioImport

        [Fact]
        public void ProcessProjectScenarioImport_Given_NothingToReplace_Then_TheSettingsAreTheDefaultsAndTheCostsAreTheImports()
        {
            var made = CoreViewModelFixture.CreateWithSettingService();
            using CoreViewModel core = made.CoreViewModel;
            ISettingService settings = made.SettingService;

            core.ProcessProjectScenarioImport(
                Import(resourceSettings: new ResourceSettingsModel { DefaultUnitCost = 7.5, DefaultUnitBilling = 9.25, AreDisabled = true }),
                Guid.NewGuid(),
                @"Imported");

            Json(core.HolidaySettings).ShouldBe(Json(settings.DefaultHolidaySettings));
            core.HolidaySettings.Holidays.Select(x => x.Id).ShouldBe(new[] { 1, 2, 3 });
            core.WorkStreamSettings.WorkStreams.ShouldBeEmpty();
            core.ResourceSettings.Resources.ShouldBeEmpty();
            core.ResourceSettings.DefaultUnitCost.ShouldBe(7.5);
            core.ResourceSettings.DefaultUnitBilling.ShouldBe(9.25);
            core.ResourceSettings.AreDisabled.ShouldBeTrue();
            Json(core.GraphSettings).ShouldBe(Json(settings.DefaultGraphSettings));
            core.GraphSettings.ActivitySeverities.Count.ShouldBe(4);
        }

        [Fact]
        public void ProcessProjectScenarioImport_Given_Holidays_Then_TheyTakeThePlaceOfTheDefaultOnes()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();

            core.ProcessProjectScenarioImport(
                Import(holidays:
                [
                    new HolidayModel { Id = 11, Name = @"Away day", RecurrencePattern = "FREQ=YEARLY;BYMONTH=6;BYMONTHDAY=1" },
                    new HolidayModel { Id = 12, Name = @"Offsite", RecurrencePattern = "FREQ=YEARLY;BYMONTH=9;BYMONTHDAY=1" },
                ]),
                Guid.NewGuid(),
                @"Imported");

            core.HolidaySettings.Holidays.Select(x => x.Id).ShouldBe(new[] { 11, 12 });
            core.HolidaySettings.Holidays.Select(x => x.Name).ShouldBe(new[] { @"Away day", @"Offsite" });
        }

        [Fact]
        public void ProcessProjectScenarioImport_Given_WorkStreams_Then_TheyTakeThePlaceOfTheDefaultOnes()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();

            core.ProcessProjectScenarioImport(
                Import(workStreams:
                [
                    new WorkStreamModel { Id = 1, Name = @"Build", DisplayOrder = 1 },
                    new WorkStreamModel { Id = 2, Name = @"Test", DisplayOrder = 2 },
                ]),
                Guid.NewGuid(),
                @"Imported");

            core.WorkStreamSettings.WorkStreams.Select(x => x.Id).ShouldBe(new[] { 1, 2 });
            core.WorkStreamSettings.WorkStreams.Select(x => x.Name).ShouldBe(new[] { @"Build", @"Test" });
        }

        [Fact]
        public void ProcessProjectScenarioImport_Given_Resources_Then_TheyTakeThePlaceOfTheDefaultOnesAndTheCostsAreTheImports()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();

            core.ProcessProjectScenarioImport(
                Import(resourceSettings: new ResourceSettingsModel
                {
                    Resources =
                    [
                        new ResourceModel { Id = 1, Name = @"Analyst", DisplayOrder = 1, UnitCost = 3.0 },
                        new ResourceModel { Id = 2, Name = @"Tester", DisplayOrder = 2, UnitCost = 4.0 },
                    ],
                    DefaultUnitCost = 2.5,
                    DefaultUnitBilling = 6.5,
                    AreDisabled = true,
                }),
                Guid.NewGuid(),
                @"Imported");

            core.ResourceSettings.Resources.Select(x => x.Name).ShouldBe(new[] { @"Analyst", @"Tester" });
            core.ResourceSettings.DefaultUnitCost.ShouldBe(2.5);
            core.ResourceSettings.DefaultUnitBilling.ShouldBe(6.5);
            core.ResourceSettings.AreDisabled.ShouldBeTrue();
        }

        [Fact]
        public void ProcessProjectScenarioImport_Given_ActivitySeverities_Then_TheyTakeThePlaceOfTheDefaultOnesAndTheFormatsStay()
        {
            var made = CoreViewModelFixture.CreateWithSettingService();
            using CoreViewModel core = made.CoreViewModel;
            ISettingService settings = made.SettingService;

            core.ProcessProjectScenarioImport(
                Import(activitySeverities: [new ActivitySeverityModel { SlackLimit = 3, CriticalityWeight = 5.0, FibonacciWeight = 8.0 }]),
                Guid.NewGuid(),
                @"Imported");

            ActivitySeverityModel severity = core.GraphSettings.ActivitySeverities.ShouldHaveSingleItem();
            severity.SlackLimit.ShouldBe(3);
            severity.CriticalityWeight.ShouldBe(5.0);
            severity.FibonacciWeight.ShouldBe(8.0);
            Json(core.GraphSettings.NodeTypeFormats).ShouldBe(Json(settings.DefaultGraphSettings.NodeTypeFormats));
            Json(core.GraphSettings.EdgeTypeFormats).ShouldBe(Json(settings.DefaultGraphSettings.EdgeTypeFormats));
            core.GraphSettings.NodeTypeFormats.ShouldNotBeEmpty();
        }

        [Fact]
        public void ProcessProjectScenarioImport_Given_AnImportAfterAnImport_Then_TheSecondDoesNotKeepWhatTheFirstReplaced()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();

            core.ProcessProjectScenarioImport(
                Import(
                    holidays: [new HolidayModel { Id = 11, Name = @"Away day", RecurrencePattern = "FREQ=YEARLY;BYMONTH=6;BYMONTHDAY=1" }],
                    workStreams: [new WorkStreamModel { Id = 1, Name = @"Build" }],
                    activitySeverities: [new ActivitySeverityModel { SlackLimit = 3 }]),
                Guid.NewGuid(),
                @"First");

            core.ProcessProjectScenarioImport(Import(), Guid.NewGuid(), @"Second");

            core.HolidaySettings.Holidays.Select(x => x.Id).ShouldBe(new[] { 1, 2, 3 });
            core.WorkStreamSettings.WorkStreams.ShouldBeEmpty();
            core.GraphSettings.ActivitySeverities.Count.ShouldBe(4);
        }

        #endregion

        #region BuildProjectScenario

        [Fact]
        public void BuildProjectScenario_Given_DependenciesInAnyOrder_Then_EachListHoldsItsIdsInOrder()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(0) with
            {
                // Activity 7 comes before 6, so that the live activity 1 holds its successors as 7, 6.
                DependentActivities =
                [
                    Model(1),
                    Model(2),
                    Model(3),
                    Model(4),
                    Model(5),
                    Model(7, dependencies: [1], displayOrder: 6),
                    Model(6, dependencies: [4, 2, 3], planningDependencies: [5, 1], displayOrder: 7),
                ],
            };
            core.ProcessProjectScenario(scenario, Guid.NewGuid(), @"Test");

            // The live activity holds the ids in the order in which they were typed.
            IManagedActivityViewModel live = core.RawActivities.Single(x => x.Id == 6);
            live.Dependencies.ShouldBe(new[] { 4, 2, 3 });
            live.PlanningDependencies.ShouldBe(new[] { 5, 1 });
            core.RawActivities.Single(x => x.Id == 1).Successors.ShouldBe(new[] { 7, 6 });

            List<DependentActivityModel> built = core.BuildProjectScenario().DependentActivities;

            DependentActivityModel six = built.Single(x => x.Activity.Id == 6);
            six.Dependencies.ShouldBe(new[] { 2, 3, 4 });
            six.PlanningDependencies.ShouldBe(new[] { 1, 5 });

            foreach (DependentActivityModel activity in built)
            {
                activity.Dependencies.ShouldBe(activity.Dependencies.Order().ToList());
                activity.PlanningDependencies.ShouldBe(activity.PlanningDependencies.Order().ToList());
                activity.ResourceDependencies.ShouldBe(activity.ResourceDependencies.Order().ToList());
                activity.Successors.ShouldBe(activity.Successors.Order().ToList());
            }

            built.Single(x => x.Activity.Id == 1).Successors.ShouldBe(new[] { 6, 7 });
        }

        [Fact]
        public void BuildProjectScenario_Given_ASortedScenario_Then_TheLiveActivitiesKeepTheirOrder()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(0) with
            {
                DependentActivities = [Model(1), Model(2), Model(3), Model(4, dependencies: [3, 1, 2])],
            };
            core.ProcessProjectScenario(scenario, Guid.NewGuid(), @"Test");

            core.BuildProjectScenario();

            // The sort is on the copy in the scenario, and not on the live activity.
            core.RawActivities.Single(x => x.Id == 4).Dependencies.ShouldBe(new[] { 3, 1, 2 });
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
                ProjectStart = s_Start,
                Today = s_Start,
                Holidays = holidays ?? [],
                WorkStreams = workStreams ?? [],
                ResourceSettings = resourceSettings ?? new ResourceSettingsModel(),
                ActivitySeverities = activitySeverities ?? [],
            };

        private static DependentActivityModel Model(
            int id,
            List<int>? dependencies = null,
            List<int>? planningDependencies = null,
            int? displayOrder = null) =>
            new()
            {
                Activity = new ActivityModel
                {
                    Id = id,
                    DisplayOrder = displayOrder ?? id,
                    Name = $@"Activity {id}",
                    Duration = 2,
                },
                Dependencies = dependencies ?? [],
                PlanningDependencies = planningDependencies ?? [],
            };

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
    }
}
