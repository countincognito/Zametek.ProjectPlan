using Shouldly;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A plan keeps the maximum latest finish date of an activity as the first day after the last day of work. With classic
    /// dates, the application shows the last day of work. The build of the scenario for a save needs the date that the plan
    /// keeps. It once got that date by a change of the display mode of the date calculator to the default mode, and back
    /// again after the activities were read. The activities, the grid and the charts share that one calculator. So each
    /// save made each activity raise its dates two times, a read on another thread between the two changes got the dates of
    /// the default mode, and a failure between them left the wrong mode for the rest of the session. The build now reads the
    /// date that the plan keeps, and it writes nothing to the calculator.
    /// </summary>
    /// <remarks>
    /// The project start is Monday 2023-05-01, and weekends do not count. Activity 3 must finish before Monday 2023-05-22,
    /// which is day 15. Its last day of work is then Friday 2023-05-19.
    /// </remarks>
    public class CoreViewModelSaveDisplayModeTests
    {
        private const int c_ActivityId = 3;

        private static readonly DateTime s_DateOfThePlan = new(2023, 5, 22);

        private static readonly DateTime s_LastDayOfWork = new(2023, 5, 19);

        [Fact]
        public void BuildProjectScenario_Given_ClassicDates_Then_NoActivityRaisesAProperty()
        {
            using CoreViewModel core = Load(useClassicDates: true);
            IManagedActivityViewModel activity = core.RawActivities.Single(x => x.Id == c_ActivityId);
            // The guard: the application shows the last day of work, so the display mode is the classic one.
            activity.MaximumLatestFinishDateTime.ShouldBe(s_LastDayOfWork);
            var raised = new List<string?>();
            foreach (INotifyPropertyChanged each in core.RawActivities.Cast<INotifyPropertyChanged>())
            {
                each.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
            }

            core.BuildProjectScenario();

            raised.ShouldBeEmpty();
            activity.MaximumLatestFinishDateTime.ShouldBe(s_LastDayOfWork);
            core.DisplaySettingsViewModel.UseClassicDates.ShouldBeTrue();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void BuildProjectScenario_Given_EachDisplayMode_Then_TheScenarioHasTheDateThatThePlanKeeps(bool useClassicDates)
        {
            using CoreViewModel core = Load(useClassicDates);

            ProjectScenarioModel scenario = core.BuildProjectScenario();

            ActivityModel activity = scenario.DependentActivities.Single(x => x.Activity.Id == c_ActivityId).Activity;
            activity.MaximumLatestFinishDateTime.ShouldNotBeNull();
            activity.MaximumLatestFinishDateTime.Value.DateTime.ShouldBe(s_DateOfThePlan);
            activity.MaximumLatestFinishTime.ShouldBe(15);
            scenario.DisplaySettings.UseClassicDates.ShouldBe(useClassicDates);
        }

        [Fact]
        public void BuildProjectScenario_Given_ClassicDatesAndADateFromTheUser_Then_TheScenarioHasTheFirstDayAfterIt()
        {
            using CoreViewModel core = Load(useClassicDates: true);
            IManagedActivityViewModel activity = core.RawActivities.Single(x => x.Id == c_ActivityId);

            // The user gives the last day of work: Monday 2023-05-22. The plan keeps the first day after it.
            activity.MaximumLatestFinishDateTime = new DateTime(2023, 5, 22);

            ProjectScenarioModel scenario = core.BuildProjectScenario();

            ActivityModel saved = scenario.DependentActivities.Single(x => x.Activity.Id == c_ActivityId).Activity;
            saved.MaximumLatestFinishDateTime.ShouldNotBeNull();
            saved.MaximumLatestFinishDateTime.Value.DateTime.ShouldBe(new DateTime(2023, 5, 23));
            activity.MaximumLatestFinishDateTime.ShouldBe(new DateTime(2023, 5, 22));
        }

        private static CoreViewModel Load(bool useClassicDates)
        {
            CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(Plan(useClassicDates), Guid.NewGuid(), @"Plan");
            return core;
        }

        // Three activities in a chain (2 + 3 + 4 days), with no resources to target. The constraint is on the last one.
        private static ProjectScenarioModel Plan(bool useClassicDates)
        {
            DateTimeOffset projectStart = Local(new DateTime(2023, 5, 1));
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(3);
            List<DependentActivityModel> activities =
            [
                .. scenario.DependentActivities.Select(x => x with
                {
                    Activity = x.Activity.Id == c_ActivityId
                        ? x.Activity with { TargetResources = [], MaximumLatestFinishDateTime = Local(s_DateOfThePlan) }
                        : x.Activity with { TargetResources = [] },
                }),
            ];

            return scenario with
            {
                ProjectStart = projectStart,
                Today = projectStart,
                DependentActivities = activities,
                DisplaySettings = scenario.DisplaySettings with
                {
                    NonWorkingDayMode = NonWorkingDayMode.Weekends,
                    UseClassicDates = useClassicDates,
                },
            };
        }

        private static DateTimeOffset Local(DateTime date)
            => new(date, TimeZoneInfo.Local.GetUtcOffset(date));
    }
}
