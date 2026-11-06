using Shouldly;
using System;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A new plan starts on today, and the core takes today from its calculator's clock rather than the
    /// machine's - so a host that fixes the time it runs at, or runs in another time zone, gets the day it
    /// asked for.
    /// </summary>
    public class CoreViewModelClockTests
    {
        // 20:00 UTC on 27 September is already 01:30 on the 28th at +05:30.
        private static readonly FixedTimeProvider s_Clock = new(
            new DateTimeOffset(2026, 9, 27, 20, 0, 0, TimeSpan.Zero),
            TimeZoneInfo.CreateCustomTimeZone(@"Test +05:30", TimeSpan.FromMinutes(330), @"Test +05:30", @"Test +05:30"));

        private static readonly (DateTime, TimeSpan) s_Today = (new DateTime(2026, 9, 28), TimeSpan.FromMinutes(330));

        [Fact]
        public void Ctor_Given_AClock_Then_ThePlanStartsOnItsDay()
        {
            using CoreViewModel core = CoreViewModelFixture.Create(clock: s_Clock);

            (core.ProjectStart.DateTime, core.ProjectStart.Offset).ShouldBe(s_Today);
            (core.Today.DateTime, core.Today.Offset).ShouldBe(s_Today);
        }

        [Fact]
        public void CreateEmptyProjectScenario_Given_AClock_Then_ItStartsOnItsDay()
        {
            using CoreViewModel core = CoreViewModelFixture.Create(clock: s_Clock);

            ProjectScenarioModel scenario = core.CreateEmptyProjectScenario();

            (scenario.ProjectStart.DateTime, scenario.ProjectStart.Offset).ShouldBe(s_Today);
            (scenario.Today.DateTime, scenario.Today.Offset).ShouldBe(s_Today);
        }
    }
}
