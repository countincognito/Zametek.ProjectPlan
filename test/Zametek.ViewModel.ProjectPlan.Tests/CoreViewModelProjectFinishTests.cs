using Shouldly;
using System;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The finish of the project as the core gives it: as text, which shows the days to it or the date it falls on as
    /// the display settings ask, and as the days and the date themselves, which a host that is not a person reading a
    /// screen - the job a server answers - can use whichever way the screen shows it.
    /// </summary>
    public class CoreViewModelProjectFinishTests
    {
        private const int c_ActivityCount = 12;

        [Fact]
        public void ProjectFinish_Given_NoPlan_Then_ThereIsNoFinish()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();

            core.ProjectFinish.ShouldBeEmpty();
            core.ProjectFinishDays.ShouldBeNull();
            core.ProjectFinishDate.ShouldBeNull();
        }

        [Fact]
        public void ProjectFinish_Given_APlanThatTakesNoTime_Then_ThereIsNoFinish()
        {
            // A plan with a duration of nothing has no day to finish on, as the text has always said.
            using CoreViewModel core = CoreViewModelFixture.Create();
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(1);
            DependentActivityModel activity = scenario.DependentActivities.Single();
            core.ProcessProjectScenario(
                scenario with { DependentActivities = [activity with { Activity = activity.Activity with { Duration = 0 } }] },
                Guid.NewGuid(),
                @"Test");
            core.RunCompile();
            core.BuildNetworkMetrics();

            (core.NetworkMetrics.Duration ?? 0).ShouldBe(0);
            core.ProjectFinish.ShouldBeEmpty();
            core.ProjectFinishDays.ShouldBeNull();
            core.ProjectFinishDate.ShouldBeNull();
        }

        [Fact]
        public void ProjectFinishDays_Given_CompiledPlan_Then_IsTheNetworksDuration()
        {
            using CoreViewModel core = CreateCompiledCore();

            int duration = core.NetworkMetrics.Duration.ShouldNotBeNull();

            duration.ShouldBeGreaterThan(0);
            core.ProjectFinishDays.ShouldBe(duration);
        }

        [Fact]
        public void ProjectFinish_Given_DaysShown_Then_TheTextIsTheDays()
        {
            using CoreViewModel core = CreateCompiledCore();

            core.DisplaySettingsViewModel.ShowDates = false;

            core.ProjectFinish.ShouldBe(core.ProjectFinishDays.ShouldNotBeNull().ToString());
        }

        [Fact]
        public void ProjectFinish_Given_DatesShown_Then_TheTextIsTheDate()
        {
            using CoreViewModel core = CreateCompiledCore();

            core.DisplaySettingsViewModel.ShowDates = true;

            DateOnly date = core.ProjectFinishDate.ShouldNotBeNull();
            core.ProjectFinish.ShouldBe(date.ToString(DateTimeCalculator.DateFormat));
        }

        [Fact]
        public void ProjectFinishDate_Given_DaysShown_Then_StillHasTheDate()
        {
            // What the screen shows is a choice of the display settings. What the finish is, is not.
            using CoreViewModel core = CreateCompiledCore();
            core.DisplaySettingsViewModel.ShowDates = true;
            DateOnly shownWithDates = core.ProjectFinishDate.ShouldNotBeNull();

            core.DisplaySettingsViewModel.ShowDates = false;

            core.ProjectFinishDate.ShouldBe(shownWithDates);
            core.ProjectFinishDays.ShouldNotBeNull();
        }

        [Fact]
        public void ProjectFinishDate_Given_EveryDayWorked_Then_IsTheStartAndTheDays()
        {
            using CoreViewModel core = CreateCompiledCore();

            int days = core.ProjectFinishDays.ShouldNotBeNull();

            core.ProjectFinishDate.ShouldBe(DateOnly.FromDateTime(core.ProjectStart.AddDays(days).DateTime));
        }

        [Fact]
        public void ProjectFinishDate_Given_ProjectStartMoved_Then_MovesWithIt()
        {
            using CoreViewModel core = CreateCompiledCore();
            DateOnly before = core.ProjectFinishDate.ShouldNotBeNull();

            core.ProjectStart = core.ProjectStart.AddDays(14);

            core.ProjectFinishDate.ShouldBe(before.AddDays(14));
            core.ProjectFinishDays.ShouldBe(core.NetworkMetrics.Duration);
        }

        [Fact]
        public void ProjectFinishDate_Given_WeekendsNotWorked_Then_FallsLater()
        {
            using CoreViewModel core = CreateCompiledCore();
            DateOnly everyDayWorked = core.ProjectFinishDate.ShouldNotBeNull();
            int days = core.ProjectFinishDays.ShouldNotBeNull();

            core.DisplaySettingsViewModel.NonWorkingDayMode = NonWorkingDayMode.Weekends;

            // The days to the finish are the same days; the calendar they are counted in has fewer of them.
            core.ProjectFinishDays.ShouldBe(days);
            core.ProjectFinishDate.ShouldNotBeNull().ShouldBeGreaterThan(everyDayWorked);
        }

        [Fact]
        public void ProjectFinishDate_Given_ClassicDates_Then_IsTheDayBefore()
        {
            // The classic display counts the finish as the last day worked rather than the day after it.
            using CoreViewModel core = CreateCompiledCore();
            DateOnly standard = core.ProjectFinishDate.ShouldNotBeNull();

            core.DisplaySettingsViewModel.UseClassicDates = true;

            core.ProjectFinishDate.ShouldBe(standard.AddDays(-1));
        }

        [Fact]
        public void ProjectFinish_Given_AnyDisplay_Then_TheTextIsMadeOfTheTypedFinish()
        {
            using CoreViewModel core = CreateCompiledCore();

            foreach (bool showDates in new[] { false, true })
            {
                foreach (bool classic in new[] { false, true })
                {
                    foreach (NonWorkingDayMode mode in new[] { NonWorkingDayMode.None, NonWorkingDayMode.Weekends })
                    {
                        core.DisplaySettingsViewModel.ShowDates = showDates;
                        core.DisplaySettingsViewModel.UseClassicDates = classic;
                        core.DisplaySettingsViewModel.NonWorkingDayMode = mode;

                        string expected = showDates
                            ? core.ProjectFinishDate.ShouldNotBeNull().ToString(DateTimeCalculator.DateFormat)
                            : core.ProjectFinishDays.ShouldNotBeNull().ToString();

                        core.ProjectFinish.ShouldBe(expected, $@"dates {showDates}, classic {classic}, {mode}");
                    }
                }
            }
        }

        // A plan that has been compiled, with its network metrics built from the compilation, as a job builds them.
        private static CoreViewModel CreateCompiledCore()
        {
            CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");
            core.RunCompile();
            core.BuildNetworkMetrics();
            return core;
        }
    }
}
