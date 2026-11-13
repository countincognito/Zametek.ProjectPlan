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
    /// A plan can give the minimum earliest start time and the maximum latest finish time of an activity as a number of
    /// working days from the project start, as a date, or as both. A load first resets the scenario, which puts the default
    /// calendar of the settings in force, and an activity that has only the number turns it into a date along the calendar
    /// in force. When the calendar changes, the activity counts the number again from that date. So the calendar of the plan
    /// has to be in force before the activities are made: a calendar that arrives after them moves each constraint that the
    /// plan gives only as a number, and each date that is not a working day of the default calendar.
    /// </summary>
    /// <remarks>
    /// The project start is Monday 2023-05-01. Day 220 is Monday 2024-03-04 when weekends do not count, and Thursday
    /// 2023-12-07 when every day counts. Counted the other way, 2023-12-07 is day 158 when weekends do not count, and
    /// 2024-03-04 is day 308 when every day counts: those were the values that a load gave when the calendar of the plan
    /// came after the activities. In the same way, a maximum latest finish time of day 10 became day 8, before the
    /// activity finishes on day 9, and the compilation failed.
    /// </remarks>
    public class CoreViewModelConstraintCalendarTests
    {
        private const int c_ActivityId = 3;

        // Activity 3 finishes on day 9, after activities 1 and 2: 2 + 3 + 4 days.
        private const int c_Finish = 9;

        private static readonly DateTimeOffset s_ProjectStart = Local(2023, 5, 1);

        #region ProcessProjectScenario

        [Theory]
        [InlineData(NonWorkingDayMode.None)]
        [InlineData(NonWorkingDayMode.Weekends)]
        public void ProcessProjectScenario_Given_AWeekendPlanWithOnlyTheNumberOfAMinimumEarliestStartTime_Then_TheNumberStays(NonWorkingDayMode defaultMode)
        {
            Constraints constraints = Load(defaultMode, Plan(NonWorkingDayMode.Weekends, minimumEarliestStartTime: 220));

            constraints.MinimumEarliestStartTime.ShouldBe(220);
            constraints.MinimumEarliestStartDateTime.ShouldBe(new DateTime(2024, 3, 4));
            constraints.EarliestStartTime.ShouldBe(220);
        }

        [Theory]
        [InlineData(NonWorkingDayMode.None)]
        [InlineData(NonWorkingDayMode.Weekends)]
        public void ProcessProjectScenario_Given_AWeekendPlanWithOnlyTheNumberOfAMaximumLatestFinishTime_Then_TheNumberStays(NonWorkingDayMode defaultMode)
        {
            Constraints constraints = Load(defaultMode, Plan(NonWorkingDayMode.Weekends, maximumLatestFinishTime: 10));

            constraints.MaximumLatestFinishTime.ShouldBe(10);
            constraints.EarliestFinishTime.ShouldBe(c_Finish);
            constraints.HasCompilationErrors.ShouldBeFalse();
        }

        [Theory]
        [InlineData(NonWorkingDayMode.None)]
        [InlineData(NonWorkingDayMode.Weekends)]
        public void ProcessProjectScenario_Given_APlanWithNoNonWorkingDaysAndOnlyTheNumberOfAMinimumEarliestStartTime_Then_TheNumberStays(NonWorkingDayMode defaultMode)
        {
            Constraints constraints = Load(defaultMode, Plan(NonWorkingDayMode.None, minimumEarliestStartTime: 220));

            constraints.MinimumEarliestStartTime.ShouldBe(220);
            constraints.MinimumEarliestStartDateTime.ShouldBe(new DateTime(2023, 12, 7));
            constraints.EarliestStartTime.ShouldBe(220);
        }

        [Theory]
        [InlineData(NonWorkingDayMode.None)]
        [InlineData(NonWorkingDayMode.Weekends)]
        public void ProcessProjectScenario_Given_APlanWithNoNonWorkingDaysAndAMinimumEarliestStartTimeOnASaturday_Then_TheDateAndTheNumberStay(NonWorkingDayMode defaultMode)
        {
            // As the app saves it: the number and the date. Every day is a working day of the plan, so the date can be a
            // Saturday, which the default calendar Weekends would move to a working day.
            Constraints constraints = Load(
                defaultMode,
                Plan(NonWorkingDayMode.None, minimumEarliestStartTime: 12, minimumEarliestStartDateTime: Local(2023, 5, 13)));

            constraints.MinimumEarliestStartTime.ShouldBe(12);
            constraints.MinimumEarliestStartDateTime.ShouldBe(new DateTime(2023, 5, 13));
            constraints.EarliestStartTime.ShouldBe(12);
        }

        [Theory]
        [InlineData(NonWorkingDayMode.None)]
        [InlineData(NonWorkingDayMode.Weekends)]
        public void ProcessProjectScenario_Given_AWeekendPlanWithTheNumberAndTheDateOfAMinimumEarliestStartTime_Then_TheyStay(NonWorkingDayMode defaultMode)
        {
            // As the app saves it.
            Constraints constraints = Load(
                defaultMode,
                Plan(NonWorkingDayMode.Weekends, minimumEarliestStartTime: 220, minimumEarliestStartDateTime: Local(2024, 3, 4)));

            constraints.MinimumEarliestStartTime.ShouldBe(220);
            constraints.MinimumEarliestStartDateTime.ShouldBe(new DateTime(2024, 3, 4));
            constraints.EarliestStartTime.ShouldBe(220);
        }

        [Theory]
        [InlineData(NonWorkingDayMode.None)]
        [InlineData(NonWorkingDayMode.Weekends)]
        public void ProcessProjectScenario_Given_AWeekendPlanWithAMinimumEarliestStartDateOnASaturday_Then_TheActivityStartsOnTheMondayAfter(NonWorkingDayMode defaultMode)
        {
            // Saturday 2023-05-13 counted as Friday 2023-05-12, day 9, so the activity could start a day before its date.
            Constraints constraints = Load(
                defaultMode,
                Plan(NonWorkingDayMode.Weekends, minimumEarliestStartDateTime: Local(2023, 5, 13)));

            constraints.MinimumEarliestStartTime.ShouldBe(10);
            constraints.MinimumEarliestStartDateTime.ShouldBe(new DateTime(2023, 5, 15));
            constraints.EarliestStartTime.ShouldBe(10);
        }

        [Theory]
        [InlineData(NonWorkingDayMode.None)]
        [InlineData(NonWorkingDayMode.Weekends)]
        public void ProcessProjectScenario_Given_AWeekendPlanWithAMaximumLatestFinishDateOnASaturday_Then_TheActivityCanFinishOnTheFriday(NonWorkingDayMode defaultMode)
        {
            // The date of a maximum latest finish is the day after the last day of work. Saturday 2023-05-13 counted as
            // Friday 2023-05-12, day 9, so the activity had to finish on the Thursday.
            Constraints constraints = Load(
                defaultMode,
                Plan(NonWorkingDayMode.Weekends, maximumLatestFinishDateTime: Local(2023, 5, 13)));

            constraints.MaximumLatestFinishTime.ShouldBe(10);
            constraints.HasCompilationErrors.ShouldBeFalse();
        }

        [Fact]
        public void MinimumEarliestStartDateTime_Given_ASaturdayInAWeekendPlan_Then_TheMondayAfter()
        {
            // As the date picker of the activities sets it.
            (CoreViewModel core, ISettingService _) = CoreViewModelFixture.CreateWithSettingService();
            using (core)
            {
                core.ProcessProjectScenario(Plan(NonWorkingDayMode.Weekends), Guid.NewGuid(), @"Plan");
                IManagedActivityViewModel activity = core.RawActivities.Single(x => x.Id == c_ActivityId);

                activity.MinimumEarliestStartDateTime = new DateTime(2023, 5, 13);

                activity.MinimumEarliestStartTime.ShouldBe(10);
                activity.MinimumEarliestStartDateTime.ShouldBe(new DateTime(2023, 5, 15));
            }
        }

        [Fact]
        public void ProcessProjectScenario_Given_APlanWhoseCalendarIsNotTheDefault_Then_TheScenarioIsNotMarkedAsUpdated()
        {
            // The calendar of the plan now arrives before the activities, but still inside the load, which does not mark
            // the scenario.
            (CoreViewModel core, ISettingService settingService) = CoreViewModelFixture.CreateWithSettingService();
            using (core)
            {
                settingService.DefaultNonWorkingDayMode = NonWorkingDayMode.None;

                core.ProcessProjectScenario(Plan(NonWorkingDayMode.Weekends, minimumEarliestStartTime: 220), Guid.NewGuid(), @"Plan");

                core.DisplaySettingsViewModel.NonWorkingDayMode.ShouldBe(NonWorkingDayMode.Weekends);
                core.IsProjectScenarioUpdated.ShouldBeFalse();
            }
        }

        #endregion

        #region ProcessProjectScenarioImport

        [Theory]
        [InlineData(NonWorkingDayMode.None)]
        [InlineData(NonWorkingDayMode.Weekends)]
        public void ProcessProjectScenarioImport_Given_AWeekendImportWithOnlyTheNumberOfAMinimumEarliestStartTime_Then_TheNumberStays(NonWorkingDayMode defaultMode)
        {
            Constraints constraints = Import(defaultMode, Plan(NonWorkingDayMode.Weekends, minimumEarliestStartTime: 220));

            constraints.MinimumEarliestStartTime.ShouldBe(220);
            constraints.MinimumEarliestStartDateTime.ShouldBe(new DateTime(2024, 3, 4));
            constraints.EarliestStartTime.ShouldBe(220);
        }

        [Theory]
        [InlineData(NonWorkingDayMode.None)]
        [InlineData(NonWorkingDayMode.Weekends)]
        public void ProcessProjectScenarioImport_Given_AnImportWithNoNonWorkingDaysAndOnlyTheNumberOfAMinimumEarliestStartTime_Then_TheNumberStays(NonWorkingDayMode defaultMode)
        {
            Constraints constraints = Import(defaultMode, Plan(NonWorkingDayMode.None, minimumEarliestStartTime: 220));

            constraints.MinimumEarliestStartTime.ShouldBe(220);
            constraints.MinimumEarliestStartDateTime.ShouldBe(new DateTime(2023, 12, 7));
            constraints.EarliestStartTime.ShouldBe(220);
        }

        [Theory]
        [InlineData(NonWorkingDayMode.None)]
        [InlineData(NonWorkingDayMode.Weekends)]
        public void ProcessProjectScenarioImport_Given_AWeekendImportWithOnlyTheNumberOfAMaximumLatestFinishTime_Then_TheNumberStays(NonWorkingDayMode defaultMode)
        {
            Constraints constraints = Import(defaultMode, Plan(NonWorkingDayMode.Weekends, maximumLatestFinishTime: 10));

            constraints.MaximumLatestFinishTime.ShouldBe(10);
            constraints.EarliestFinishTime.ShouldBe(c_Finish);
            constraints.HasCompilationErrors.ShouldBeFalse();
        }

        #endregion

        #region Helpers

        private sealed record Constraints(
            int? MinimumEarliestStartTime,
            DateTime? MinimumEarliestStartDateTime,
            int? MaximumLatestFinishTime,
            int? EarliestStartTime,
            int? EarliestFinishTime,
            bool HasCompilationErrors);

        // Loads the plan with the default calendar of the settings that the test asks for, as the desktop app does with the
        // default of the user. zpp always has the default None.
        private static Constraints Load(NonWorkingDayMode defaultMode, ProjectScenarioModel plan)
        {
            (CoreViewModel core, ISettingService settingService) = CoreViewModelFixture.CreateWithSettingService();
            using (core)
            {
                settingService.DefaultNonWorkingDayMode = defaultMode;
                core.ProcessProjectScenario(plan, Guid.NewGuid(), @"Plan");
                return ConstraintsOf(core);
            }
        }

        // Imports the same activities, with the same calendar and project start, as an import from a file gives them.
        private static Constraints Import(NonWorkingDayMode defaultMode, ProjectScenarioModel plan)
        {
            (CoreViewModel core, ISettingService settingService) = CoreViewModelFixture.CreateWithSettingService();
            using (core)
            {
                settingService.DefaultNonWorkingDayMode = defaultMode;
                core.ProcessProjectScenarioImport(
                    new ProjectScenarioImportModel
                    {
                        ProjectStart = plan.ProjectStart,
                        Today = plan.Today,
                        DependentActivities = plan.DependentActivities,
                        DisplaySettings = plan.DisplaySettings,
                    },
                    Guid.NewGuid(),
                    @"Import");
                return ConstraintsOf(core);
            }
        }

        private static Constraints ConstraintsOf(CoreViewModel core)
        {
            IManagedActivityViewModel activity = core.RawActivities.Single(x => x.Id == c_ActivityId);
            return new Constraints(
                activity.MinimumEarliestStartTime,
                activity.MinimumEarliestStartDateTime,
                activity.MaximumLatestFinishTime,
                activity.EarliestStartTime,
                activity.EarliestFinishTime,
                core.HasCompilationErrors);
        }

        // Three activities in a chain, with no resources to target. The constraints are on the last one.
        private static ProjectScenarioModel Plan(
            NonWorkingDayMode mode,
            int? minimumEarliestStartTime = null,
            DateTimeOffset? minimumEarliestStartDateTime = null,
            int? maximumLatestFinishTime = null,
            DateTimeOffset? maximumLatestFinishDateTime = null)
        {
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(3);
            List<DependentActivityModel> activities =
            [
                .. scenario.DependentActivities.Select(x => x with
                {
                    Activity = x.Activity.Id == c_ActivityId
                        ? x.Activity with
                        {
                            TargetResources = [],
                            MinimumEarliestStartTime = minimumEarliestStartTime,
                            MinimumEarliestStartDateTime = minimumEarliestStartDateTime,
                            MaximumLatestFinishTime = maximumLatestFinishTime,
                            MaximumLatestFinishDateTime = maximumLatestFinishDateTime,
                        }
                        : x.Activity with { TargetResources = [] },
                }),
            ];
            activities.Sum(x => x.Activity.Duration).ShouldBe(c_Finish);

            return scenario with
            {
                ProjectStart = s_ProjectStart,
                Today = s_ProjectStart,
                DependentActivities = activities,
                DisplaySettings = scenario.DisplaySettings with { NonWorkingDayMode = mode },
            };
        }

        private static DateTimeOffset Local(int year, int month, int day) =>
            new(
                new DateTime(year, month, day),
                TimeZoneInfo.Local.GetUtcOffset(new DateTime(year, month, day)));

        #endregion
    }
}
