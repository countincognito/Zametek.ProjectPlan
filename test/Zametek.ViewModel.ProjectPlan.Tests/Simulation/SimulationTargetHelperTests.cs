using Shouldly;
using System;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The target of a simulation can be a date. People give a deadline as a date, and "done by June 30" means "done by the
    /// end of June 30". So the target date is the last day on which work can be done, and its finish time is the number of
    /// working days from the start of the project up to and including that date. A maximum latest finish date of an
    /// activity is the first day after the last day of work: the same deadline is the date of the next day there.
    /// </summary>
    /// <remarks>
    /// The project start is Monday 2027-01-04.
    /// </remarks>
    public class SimulationTargetHelperTests
    {
        private static readonly DateTimeOffset s_ProjectStart = Day(2027, 1, 4);

        [Theory]
        [InlineData(2027, 1, 4, 1)] // The first day: one working day.
        [InlineData(2027, 1, 8, 5)] // Friday of the first week.
        [InlineData(2027, 1, 9, 5)] // Saturday: no more work than by the end of Friday.
        [InlineData(2027, 1, 10, 5)] // Sunday.
        [InlineData(2027, 1, 11, 6)] // Monday of the second week.
        [InlineData(2027, 1, 15, 10)]
        [InlineData(2027, 6, 30, 128)] // Wednesday 2027-06-30.
        public void FinishTimeByEndOf_Given_APlanWithWeekends_Then_IsTheWorkingDaysUpToAndIncludingTheDate(int year, int month, int day, int expected)
        {
            DateTimeCalculator calculator = Calculator(NonWorkingDayMode.Weekends);

            SimulationTargetHelper.FinishTimeByEndOf(calculator, s_ProjectStart, Day(year, month, day)).ShouldBe(expected);
        }

        [Theory]
        [InlineData(2027, 1, 4, 1)]
        [InlineData(2027, 1, 9, 6)]
        [InlineData(2027, 1, 15, 12)]
        public void FinishTimeByEndOf_Given_APlanWhereEachDayIsAWorkingDay_Then_IsTheDaysUpToAndIncludingTheDate(int year, int month, int day, int expected)
        {
            DateTimeCalculator calculator = Calculator(NonWorkingDayMode.None);

            SimulationTargetHelper.FinishTimeByEndOf(calculator, s_ProjectStart, Day(year, month, day)).ShouldBe(expected);
        }

        [Theory]
        [InlineData(2027, 1, 3)] // The day before the start: no day of work is before its end.
        [InlineData(2026, 6, 30)]
        public void FinishTimeByEndOf_Given_ADateBeforeTheStart_Then_IsDay0(int year, int month, int day)
        {
            DateTimeCalculator calculator = Calculator(NonWorkingDayMode.Weekends);

            SimulationTargetHelper.FinishTimeByEndOf(calculator, s_ProjectStart, Day(year, month, day)).ShouldBe(0);
        }

        [Fact]
        public void FinishTimeByEndOf_Given_ADate_Then_IsTheTimeOfAMaximumLatestFinishDateOfTheNextDay()
        {
            DateTimeCalculator calculator = Calculator(NonWorkingDayMode.Weekends);

            foreach (int offset in Enumerable.Range(0, 60))
            {
                DateTimeOffset date = s_ProjectStart.AddDays(offset);
                (int? time, _) = calculator.CalculateConstraintTimeAndDateTime(s_ProjectStart, date.AddDays(1));

                time.ShouldBe(SimulationTargetHelper.FinishTimeByEndOf(calculator, s_ProjectStart, date));
            }
        }

        [Fact]
        public void Resolve_Given_ADateAndANumber_Then_TheDateTakesThePlaceOfTheNumber()
        {
            DateTimeCalculator calculator = Calculator(NonWorkingDayMode.Weekends);
            var settings = new SimulationSettingsModel { TargetFinishTime = 3, TargetFinishDateTime = Day(2027, 1, 15) };

            SimulationTargetHelper.Resolve(settings, calculator, s_ProjectStart).ShouldBe(10);
            SimulationTargetHelper.Normalize(settings, calculator, s_ProjectStart)
                .ShouldBe(settings with { TargetFinishTime = 10 });
        }

        [Fact]
        public void Resolve_Given_OnlyANumber_Then_TheNumber()
        {
            DateTimeCalculator calculator = Calculator(NonWorkingDayMode.Weekends);
            var settings = new SimulationSettingsModel { TargetFinishTime = 3 };

            SimulationTargetHelper.Resolve(settings, calculator, s_ProjectStart).ShouldBe(3);
            SimulationTargetHelper.Normalize(settings, calculator, s_ProjectStart).ShouldBeSameAs(settings);
        }

        [Fact]
        public void Resolve_Given_NoTarget_Then_None()
        {
            DateTimeCalculator calculator = Calculator(NonWorkingDayMode.Weekends);
            var settings = new SimulationSettingsModel();

            SimulationTargetHelper.Resolve(settings, calculator, s_ProjectStart).ShouldBeNull();
            SimulationTargetHelper.Normalize(settings, calculator, s_ProjectStart).ShouldBeSameAs(settings);
        }

        [Fact]
        public void BuildProjectScenario_Given_ATargetDate_Then_TheScenarioHasTheDateAndItsNumber()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            ProjectScenarioModel plan = CoreViewModelFixture.CreateProjectScenario(3);
            core.ProcessProjectScenario(
                plan with
                {
                    ProjectStart = s_ProjectStart,
                    Today = s_ProjectStart,
                    DisplaySettings = plan.DisplaySettings with { NonWorkingDayMode = NonWorkingDayMode.Weekends },
                    SimulationSettings = new SimulationSettingsModel { TargetFinishTime = 3, TargetFinishDateTime = Day(2027, 1, 15) },
                },
                Guid.NewGuid(),
                @"Plan");

            SimulationSettingsModel saved = core.BuildProjectScenario().SimulationSettings;

            saved.TargetFinishDateTime.ShouldBe(Day(2027, 1, 15));
            saved.TargetFinishTime.ShouldBe(10);
        }

        private static DateTimeCalculator Calculator(NonWorkingDayMode mode) =>
            new(TimeProvider.System) { NonWorkingDayMode = mode };

        private static DateTimeOffset Day(int year, int month, int day)
        {
            var date = new DateTime(year, month, day);
            return new DateTimeOffset(date, TimeZoneInfo.Local.GetUtcOffset(date));
        }
    }
}
