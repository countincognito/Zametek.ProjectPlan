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
    /// The plan keeps the start that the user gives: <c>RequestedProjectStart</c> of the core, and <c>ProjectStart</c> in
    /// the file. Day 0 of the schedule is the first working day on that date or after it: <c>ProjectStart</c> of the core,
    /// and <c>EffectiveProjectStart</c> in the file. A change of the calendar moves only the effective start, and moves it
    /// back when the change goes. A calendar with no working day makes the compile fail with P0080, and it does not make
    /// the arithmetic of the dates search without end.
    /// </summary>
    /// <remarks>
    /// Before the effective start, day 0 was the start that the plan gave, also on a Saturday: with weekends, a plan that
    /// started on a Saturday did its first day of work on that Saturday, and each date was one working day early.
    /// </remarks>
    public class CoreViewModelEffectiveProjectStartTests
    {
        private const int c_ActivityId = 3;

        private static readonly DateTimeOffset s_Saturday = Local(2023, 4, 29);
        private static readonly DateTimeOffset s_Monday = Local(2023, 5, 1);

        [Fact]
        public void ProcessProjectScenario_Given_AWeekendPlanThatStartsOnASaturday_Then_DayNoughtIsTheMondayAfter()
        {
            using CoreViewModel core = Load(Plan(NonWorkingDayMode.Weekends, s_Saturday));

            core.RequestedProjectStart.ShouldBe(s_Saturday);
            core.ProjectStart.ShouldBe(s_Monday);
        }

        [Fact]
        public void ProcessProjectScenario_Given_AWeekendPlanThatStartsOnASaturday_Then_ItFinishesWithThePlanThatStartsOnTheMonday()
        {
            DateOnly fromSaturday;
            DateOnly fromMonday;
            using (CoreViewModel core = Load(Plan(NonWorkingDayMode.Weekends, s_Saturday)))
            {
                fromSaturday = core.ProjectFinishDate.ShouldNotBeNull();
            }
            using (CoreViewModel core = Load(Plan(NonWorkingDayMode.Weekends, s_Monday)))
            {
                fromMonday = core.ProjectFinishDate.ShouldNotBeNull();
            }

            fromSaturday.ShouldBe(fromMonday);
        }

        [Fact]
        public void BuildProjectScenario_Given_AStartOnASaturday_Then_TheFileKeepsTheRequestedStartAndHasTheEffectiveOne()
        {
            using CoreViewModel core = Load(Plan(NonWorkingDayMode.Weekends, s_Saturday));

            ProjectScenarioModel saved = core.BuildProjectScenario();

            saved.ProjectStart.ShouldBe(s_Saturday);
            saved.EffectiveProjectStart.ShouldBe(s_Monday);
        }

        [Fact]
        public void ProcessProjectScenario_Given_AStartOnASaturdayWithNoNonWorkingDays_Then_TheEffectiveStartIsThatSaturday()
        {
            using CoreViewModel core = Load(Plan(NonWorkingDayMode.None, s_Saturday));

            core.ProjectStart.ShouldBe(s_Saturday);
        }

        [Fact]
        public void HolidaySettings_Given_AHolidayOnTheRequestedStart_Then_TheEffectiveStartMovesAndComesBack()
        {
            // A custom calendar with no holidays: each day is a working day.
            using CoreViewModel core = Load(Plan(NonWorkingDayMode.CustomCalendar, s_Monday));
            core.ProjectStart.ShouldBe(s_Monday);

            core.HolidaySettings = HolidaysOn(s_Monday);

            core.RequestedProjectStart.ShouldBe(s_Monday);
            core.ProjectStart.ShouldBe(Local(2023, 5, 2));

            core.HolidaySettings = new HolidaySettingsModel();

            core.ProjectStart.ShouldBe(s_Monday);
        }

        [Fact]
        public void NonWorkingDayMode_Given_AStartOnASaturday_Then_TheActivitiesCountFromTheMondayAfter()
        {
            using CoreViewModel core = Load(Plan(NonWorkingDayMode.None, s_Saturday));
            core.ProjectStart.ShouldBe(s_Saturday);

            core.DisplaySettingsViewModel.NonWorkingDayMode = NonWorkingDayMode.Weekends;

            core.RequestedProjectStart.ShouldBe(s_Saturday);
            core.ProjectStart.ShouldBe(s_Monday);
            core.RawActivities.Select(x => x.ProjectStart).ShouldAllBe(x => x == s_Monday);
        }

        [Fact]
        public void ProcessProjectScenario_Given_AFileWithAWrongEffectiveStart_Then_TheLoadCalculatesItAgain()
        {
            ProjectScenarioModel plan = Plan(NonWorkingDayMode.Weekends, s_Saturday) with { EffectiveProjectStart = Local(2023, 6, 1) };

            using CoreViewModel core = Load(plan);

            core.ProjectStart.ShouldBe(s_Monday);
            core.BuildProjectScenario().EffectiveProjectStart.ShouldBe(s_Monday);
        }

        [Fact]
        public void RequestedProjectStart_Given_ASaturday_Then_TheActivitiesCountFromTheMondayAfter()
        {
            // The constraint is on Wednesday 2023-05-10. From Monday 2023-05-08, it is day 2.
            using CoreViewModel core = Load(Plan(NonWorkingDayMode.Weekends, s_Monday, minimumEarliestStartDateTime: Local(2023, 5, 10)));

            core.RequestedProjectStart = Local(2023, 5, 6);

            core.ProjectStart.ShouldBe(Local(2023, 5, 8));
            IManagedActivityViewModel activity = core.RawActivities.Single(x => x.Id == c_ActivityId);
            activity.MinimumEarliestStartTime.ShouldBe(2);
            activity.MinimumEarliestStartDateTime.ShouldBe(new DateTime(2023, 5, 10));
        }

        [Fact]
        public void ProcessProjectScenario_Given_ACalendarWithNoWorkingDay_Then_TheCompileFailsWithP0080()
        {
            ProjectScenarioModel plan = Plan(NonWorkingDayMode.CustomCalendar, s_Monday) with { HolidaySettings = DaysOffFrom(s_Monday) };

            (bool hasErrors, List<GraphCompilationErrorCode> codes, DateTimeOffset start) = WithinAMinute(() =>
            {
                using CoreViewModel core = Load(plan);
                return (core.HasCompilationErrors, core.GraphCompilation.CompilationErrors.Select(x => x.ErrorCode).ToList(), core.ProjectStart);
            });

            hasErrors.ShouldBeTrue();
            codes.ShouldBe([GraphCompilationErrorCode.P0080]);
            start.ShouldBe(s_Monday);
        }

        [Fact]
        public void ProcessProjectScenario_Given_ACalendarWhoseWorkingDaysStop_Then_TheCompileFailsWithP0080()
        {
            // Two working days, and then each day is a day off: the activities cannot finish in the calendar.
            ProjectScenarioModel plan = Plan(NonWorkingDayMode.CustomCalendar, s_Monday) with { HolidaySettings = DaysOffFrom(Local(2023, 5, 3)) };

            List<GraphCompilationErrorCode> codes = WithinAMinute(() =>
            {
                using CoreViewModel core = Load(plan);
                return core.GraphCompilation.CompilationErrors.Select(x => x.ErrorCode).ToList();
            });

            codes.ShouldBe([GraphCompilationErrorCode.P0080]);
        }

        #region Helpers

        // Each search for a working day stops after DateTimeCalculator.MaximumNonWorkingRun days. A search without end fails
        // the test after a minute instead of hanging the run.
        private static T WithinAMinute<T>(Func<T> work)
        {
            Task<T> task = Task.Run(work);
            task.Wait(TimeSpan.FromMinutes(1)).ShouldBeTrue(@"the search for a working day must end");
            return task.Result;
        }

        private static CoreViewModel Load(ProjectScenarioModel plan)
        {
            CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(plan, Guid.NewGuid(), @"Plan");
            core.RunCompile();
            core.BuildNetworkMetrics();
            return core;
        }

        // Three activities in a chain, with no resources to target. A constraint, if any, is on the last one.
        private static ProjectScenarioModel Plan(
            NonWorkingDayMode mode,
            DateTimeOffset projectStart,
            DateTimeOffset? minimumEarliestStartDateTime = null)
        {
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(3);
            return scenario with
            {
                ProjectStart = projectStart,
                Today = projectStart,
                DependentActivities =
                [
                    .. scenario.DependentActivities.Select(x => x with
                    {
                        Activity = x.Activity.Id == c_ActivityId
                            ? x.Activity with { TargetResources = [], MinimumEarliestStartDateTime = minimumEarliestStartDateTime }
                            : x.Activity with { TargetResources = [] },
                    }),
                ],
                DisplaySettings = scenario.DisplaySettings with { NonWorkingDayMode = mode },
            };
        }

        private static HolidaySettingsModel HolidaysOn(DateTimeOffset day) =>
            new()
            {
                Holidays = [new HolidayModel { Id = 1, StartDateTime = day, RecurrencePattern = @"FREQ=DAILY;COUNT=1" }],
            };

        // Each day from the given day is a day off.
        private static HolidaySettingsModel DaysOffFrom(DateTimeOffset day) =>
            new()
            {
                Holidays = [new HolidayModel { Id = 1, StartDateTime = day, RecurrencePattern = @"FREQ=DAILY" }],
            };

        private static DateTimeOffset Local(int year, int month, int day) =>
            new(
                new DateTime(year, month, day),
                TimeZoneInfo.Local.GetUtcOffset(new DateTime(year, month, day)));

        #endregion
    }
}
