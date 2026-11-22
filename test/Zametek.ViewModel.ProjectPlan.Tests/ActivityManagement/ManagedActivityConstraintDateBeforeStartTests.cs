using Shouldly;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The calendar gives a constraint date before the start of the project day 0, and the activity shows the start in place
    /// of the date. Nothing said so, and a date before the start is usually a mistake, for example the wrong year (phase 3 of
    /// the zpp skill). The activity now shows a warning on the constraint, where it shows the errors of its constraints. The
    /// warning stays until the constraint changes.
    /// </summary>
    public class ManagedActivityConstraintDateBeforeStartTests
    {
        private const int c_ActivityId = 3;

        private static readonly DateTimeOffset s_ProjectStart = Local(2023, 5, 1);

        [Fact]
        public void MaximumLatestFinishDateTime_Given_ADateBeforeTheStart_Then_DayZeroWithAWarning()
        {
            (CoreViewModel core, IManagedActivityViewModel activity) = Load(Plan());
            using (core)
            {
                activity.MaximumLatestFinishDateTime = new DateTime(2022, 5, 1);

                activity.MaximumLatestFinishTime.ShouldBe(0);
                ErrorsOf(activity, nameof(IManagedActivityViewModel.MaximumLatestFinishDateTime)).ShouldBe([Warning()]);
                ErrorsOf(activity, nameof(IManagedActivityViewModel.MaximumLatestFinishTime)).ShouldBe([Warning()]);
            }
        }

        [Fact]
        public void MinimumEarliestStartDateTime_Given_ADateBeforeTheStart_Then_DayZeroWithAWarning()
        {
            (CoreViewModel core, IManagedActivityViewModel activity) = Load(Plan());
            using (core)
            {
                activity.MinimumEarliestStartDateTime = new DateTime(2022, 5, 1);

                activity.MinimumEarliestStartTime.ShouldBe(0);
                ErrorsOf(activity, nameof(IManagedActivityViewModel.MinimumEarliestStartDateTime)).ShouldBe([Warning()]);
                ErrorsOf(activity, nameof(IManagedActivityViewModel.MinimumEarliestStartTime)).ShouldBe([Warning()]);
            }
        }

        [Fact]
        public void MaximumLatestFinishDateTime_Given_ADateAfterTheStartAfterADateBeforeIt_Then_NoWarning()
        {
            (CoreViewModel core, IManagedActivityViewModel activity) = Load(Plan());
            using (core)
            {
                activity.MaximumLatestFinishDateTime = new DateTime(2022, 5, 1);
                activity.MaximumLatestFinishDateTime = new DateTime(2023, 6, 1);

                ErrorsOf(activity, nameof(IManagedActivityViewModel.MaximumLatestFinishDateTime)).ShouldBeEmpty();
                ErrorsOf(activity, nameof(IManagedActivityViewModel.MaximumLatestFinishTime)).ShouldBeEmpty();
            }
        }

        [Fact]
        public void MaximumLatestFinishTime_Given_ANumberAfterADateBeforeTheStart_Then_NoWarning()
        {
            // A number counts from the start, and so it never gives a date before it.
            (CoreViewModel core, IManagedActivityViewModel activity) = Load(Plan());
            using (core)
            {
                activity.MaximumLatestFinishDateTime = new DateTime(2022, 5, 1);
                activity.MaximumLatestFinishTime = 20;

                ErrorsOf(activity, nameof(IManagedActivityViewModel.MaximumLatestFinishDateTime)).ShouldBeEmpty();
                ErrorsOf(activity, nameof(IManagedActivityViewModel.MaximumLatestFinishTime)).ShouldBeEmpty();
            }
        }

        [Fact]
        public void Duration_Given_AWarningOfADateBeforeTheStart_Then_TheWarningStays()
        {
            // A change to the duration checks the constraints again, and the check clears each error of the constraints.
            (CoreViewModel core, IManagedActivityViewModel activity) = Load(Plan());
            using (core)
            {
                activity.MinimumEarliestStartDateTime = new DateTime(2022, 5, 1);
                activity.Duration = 6;

                ErrorsOf(activity, nameof(IManagedActivityViewModel.MinimumEarliestStartDateTime)).ShouldBe([Warning()]);
            }
        }

        [Fact]
        public void MinimumEarliestStartDateTime_Given_AWarningAndAnErrorOfTheConstraints_Then_ADateAfterTheStartRemovesOnlyTheWarning()
        {
            // A minimum EST of day 0 and a maximum LFT of day 2 leave less room than the duration of 4 days.
            string error = Resource.ProjectPlan.Labels.Label_MinimumEarliestStartTimeToMaximumLatestFinishTimeMustBeGreaterThanOrEqualToDuration;
            (CoreViewModel core, IManagedActivityViewModel activity) = Load(Plan());
            using (core)
            {
                activity.MinimumEarliestStartDateTime = new DateTime(2022, 5, 1);
                activity.MaximumLatestFinishTime = 2;

                ErrorsOf(activity, nameof(IManagedActivityViewModel.MinimumEarliestStartDateTime)).ShouldBe([error, Warning()], ignoreOrder: true);

                activity.MinimumEarliestStartDateTime = new DateTime(2023, 5, 2);

                activity.MinimumEarliestStartTime.ShouldBe(1);
                ErrorsOf(activity, nameof(IManagedActivityViewModel.MinimumEarliestStartDateTime)).ShouldBe([error]);
            }
        }

        [Fact]
        public void ProcessProjectScenario_Given_ConstraintDatesBeforeTheStart_Then_AWarningOnEach()
        {
            // A load does not check the constraints, but it gives these warnings.
            (CoreViewModel core, IManagedActivityViewModel activity) = Load(Plan(minimumEarliestStartDateTime: Local(2022, 5, 1), maximumLatestFinishDateTime: Local(2022, 6, 1)));
            using (core)
            {
                activity.MinimumEarliestStartTime.ShouldBe(0);
                activity.MaximumLatestFinishTime.ShouldBe(0);
                ErrorsOf(activity, nameof(IManagedActivityViewModel.MinimumEarliestStartDateTime)).ShouldBe([Warning()]);
                ErrorsOf(activity, nameof(IManagedActivityViewModel.MaximumLatestFinishDateTime)).ShouldBe([Warning()]);
            }
        }

        [Fact]
        public void ProcessProjectScenario_Given_ConstraintDatesOnTheStartAndAfterIt_Then_NoWarning()
        {
            (CoreViewModel core, IManagedActivityViewModel activity) = Load(Plan(minimumEarliestStartDateTime: s_ProjectStart, maximumLatestFinishDateTime: Local(2023, 6, 1)));
            using (core)
            {
                ErrorsOf(activity, nameof(IManagedActivityViewModel.MinimumEarliestStartDateTime)).ShouldBeEmpty();
                ErrorsOf(activity, nameof(IManagedActivityViewModel.MaximumLatestFinishDateTime)).ShouldBeEmpty();
            }
        }

        [Fact]
        public void ProcessProjectScenarioImport_Given_AConstraintDateBeforeTheStart_Then_AWarning()
        {
            ProjectScenarioModel plan = Plan(maximumLatestFinishDateTime: Local(2022, 6, 1));
            (CoreViewModel core, ISettingService _) = CoreViewModelFixture.CreateWithSettingService();
            using (core)
            {
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
                IManagedActivityViewModel activity = core.RawActivities.Single(x => x.Id == c_ActivityId);

                activity.MaximumLatestFinishTime.ShouldBe(0);
                ErrorsOf(activity, nameof(IManagedActivityViewModel.MaximumLatestFinishDateTime)).ShouldBe([Warning()]);
            }
        }

        #region Helpers

        // The warning names the start of the project, in the short date of the culture.
        private static string Warning() =>
            string.Format(
                CultureInfo.CurrentCulture,
                Resource.ProjectPlan.Labels.Label_ConstraintDateBeforeProjectStart,
                s_ProjectStart.ToString(@"d", CultureInfo.CurrentCulture));

        private static List<string> ErrorsOf(IManagedActivityViewModel activity, string propertyName) =>
            [.. ((INotifyDataErrorInfo)activity).GetErrors(propertyName).Cast<string>()];

        private static (CoreViewModel Core, IManagedActivityViewModel Activity) Load(ProjectScenarioModel plan)
        {
            (CoreViewModel core, ISettingService _) = CoreViewModelFixture.CreateWithSettingService();
            core.ProcessProjectScenario(plan, Guid.NewGuid(), @"Plan");
            return (core, core.RawActivities.Single(x => x.Id == c_ActivityId));
        }

        // Three activities in a chain of 2, 3 and 4 days, with no calendar and no resources to target. The constraints are on
        // the last one.
        private static ProjectScenarioModel Plan(
            DateTimeOffset? minimumEarliestStartDateTime = null,
            DateTimeOffset? maximumLatestFinishDateTime = null)
        {
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(3);
            return scenario with
            {
                ProjectStart = s_ProjectStart,
                Today = s_ProjectStart,
                DependentActivities =
                [
                    .. scenario.DependentActivities.Select(x => x with
                    {
                        Activity = x.Activity.Id == c_ActivityId
                            ? x.Activity with
                            {
                                TargetResources = [],
                                MinimumEarliestStartDateTime = minimumEarliestStartDateTime,
                                MaximumLatestFinishDateTime = maximumLatestFinishDateTime,
                            }
                            : x.Activity with { TargetResources = [] },
                    }),
                ],
                DisplaySettings = scenario.DisplaySettings with { NonWorkingDayMode = NonWorkingDayMode.None },
            };
        }

        private static DateTimeOffset Local(int year, int month, int day) =>
            new(
                new DateTime(year, month, day),
                TimeZoneInfo.Local.GetUtcOffset(new DateTime(year, month, day)));

        #endregion
    }
}
