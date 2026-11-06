using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// An activity's minimum earliest start time and maximum latest finish time are
    /// compiler inputs derived from a date, so they have to be recalculated by the time
    /// the change that invalidated them returns - not on a later turn of the scheduler.
    /// </summary>
    /// <remarks>
    /// Three things invalidate them: the project start they are measured from, the
    /// holidays the calendar excludes, and the non-working day mode that decides whether
    /// the calendar is consulted at all. Each used to reach the activity through a
    /// deferred subscription observing on <c>Scheduler.CurrentThread</c>, which runs
    /// inline only while nothing else holds that thread's trampoline; when something
    /// does - a write made from inside another scheduled delivery, which is the ordinary
    /// case once the cascade is running - the delivery is queued behind the action
    /// instead. The setter that queued it then armed a compile, so a compile could be
    /// started against a plan whose date-derived inputs still belonged to the previous
    /// calendar (ARCHITECTURE section 7 rule 10: live activity state is compiler state
    /// and is never written from a deferred callback).
    /// <para>
    /// Each test therefore makes its change from inside an active trampoline and reads
    /// the result before that action returns. Asserting after the trampoline drains would
    /// pass either way and prove nothing.
    /// </para>
    /// </remarks>
    public class ActivityDateTimeDeliveryTests
    {
        private const int c_ActivityId = 1;

        // A Monday and the Thursday of the same week, so no weekend falls between them
        // and a count only changes when a holiday is introduced deliberately.
        private static readonly DateTimeOffset s_Monday = Local(2026, 1, 5);
        private static readonly DateTimeOffset s_Tuesday = Local(2026, 1, 6);
        private static readonly DateTimeOffset s_Thursday = Local(2026, 1, 8);

        [Fact]
        public void ProjectStartChange_IsAppliedToTheActivityBeforeTheSetterReturns()
        {
            using CoreViewModel core = CreateCoreWithOneConstrainedActivity();
            IManagedActivityViewModel activity = core.RawActivities.Single();

            // Monday to Thursday, every day counted.
            activity.MinimumEarliestStartTime.ShouldBe(3);

            int? observed = null;

            Scheduler.CurrentThread.Schedule(() =>
            {
                core.ProjectStart = s_Tuesday;
                observed = activity.MinimumEarliestStartTime;
            });

            observed.ShouldBe(
                2,
                @"the activity's minimum earliest start time must be measured from the new project start before the setter that armed the compile returns");
        }

        [Fact]
        public void HolidaySettingsChange_IsAppliedToTheActivityBeforeTheSetterReturns()
        {
            using CoreViewModel core = CreateCoreWithOneConstrainedActivity();
            IManagedActivityViewModel activity = core.RawActivities.Single();

            // The calendar is consulted from here on, but it is still empty.
            core.DisplaySettingsViewModel.NonWorkingDayMode = NonWorkingDayMode.CustomCalendar;
            activity.MinimumEarliestStartTime.ShouldBe(3);

            int? observed = null;

            Scheduler.CurrentThread.Schedule(() =>
            {
                core.HolidaySettings = HolidaysOn(s_Tuesday);
                observed = activity.MinimumEarliestStartTime;
            });

            observed.ShouldBe(
                2,
                @"the activity's minimum earliest start time must exclude the new holiday before the setter that armed the compile returns");
        }

        [Fact]
        public void NonWorkingDayModeChange_IsAppliedToTheActivityBeforeTheSetterReturns()
        {
            using CoreViewModel core = CreateCoreWithOneConstrainedActivity();
            IManagedActivityViewModel activity = core.RawActivities.Single();

            // A holiday the calculator is not yet looking at, because the mode is None.
            core.HolidaySettings = HolidaysOn(s_Tuesday);
            activity.MinimumEarliestStartTime.ShouldBe(3);

            int? observed = null;

            Scheduler.CurrentThread.Schedule(() =>
            {
                core.DisplaySettingsViewModel.NonWorkingDayMode = NonWorkingDayMode.CustomCalendar;
                observed = activity.MinimumEarliestStartTime;
            });

            observed.ShouldBe(
                2,
                @"the activity's minimum earliest start time must be recalculated against the calendar before the setter that armed the compile returns");
        }

        /// <summary>
        /// One activity held off the project start by a fixed date. The date is what the
        /// plan stores; the integer the compiler reads is derived from it and the
        /// calendar, which is what makes it go stale.
        /// </summary>
        private static CoreViewModel CreateCoreWithOneConstrainedActivity()
        {
            CoreViewModel core = CoreViewModelFixture.Create();

            core.ProjectStart = s_Monday;
            core.GraphSettings = CoreViewModelFixture.DefaultGraphSettings;
            core.AddManagedActivities(
                [
                    new DependentActivityModel
                    {
                        Activity = new ActivityModel
                        {
                            Id = c_ActivityId,
                            DisplayOrder = 1,
                            Name = @"Activity 1",
                            Duration = 1,
                            MinimumEarliestStartDateTime = s_Thursday,
                        },
                    },
                ]);

            return core;
        }

        private static HolidaySettingsModel HolidaysOn(DateTimeOffset day) =>
            new()
            {
                Holidays =
                [
                    new HolidayModel
                    {
                        Id = 1,
                        StartDateTime = day,
                        RecurrencePattern = @"FREQ=DAILY;COUNT=1",
                    },
                ],
            };

        private static DateTimeOffset Local(int year, int month, int day) =>
            new(
                new DateTime(year, month, day),
                TimeZoneInfo.Local.GetUtcOffset(new DateTime(year, month, day)));
    }
}
