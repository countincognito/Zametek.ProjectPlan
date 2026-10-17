using Shouldly;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The three estimates of an activity's duration: the optimistic duration (O), the most likely
    /// duration (ML) and the pessimistic duration (P).
    /// </summary>
    /// <remarks>
    /// The rules are 0 &lt;= O &lt;= ML &lt;= P, and 0, 0 when ML is 0. An O or a P that the user did not
    /// give is null and follows ML. The duration that the compiler schedules is the PERT expected
    /// duration, (O + 4 ML + P) / 6, rounded to the nearest whole day with halves up, so an activity
    /// with no estimates of its own is scheduled exactly as it was before the estimates existed.
    /// </remarks>
    public class ActivityEstimatesTests
    {
        private const int c_Id = 1;

        #region The rules

        [Theory]
        [InlineData(0, 0, 0, 0)]
        [InlineData(2, 4, 6, 4)]
        [InlineData(4, 5, 12, 6)]
        [InlineData(1, 5, 9, 5)]
        [InlineData(9, 10, 17, 11)]
        // A sixth: a third rounds down, and a half rounds up.
        [InlineData(0, 0, 2, 0)]
        [InlineData(0, 0, 3, 1)]
        [InlineData(0, 1, 5, 2)]
        [InlineData(1, 2, 2, 2)]
        [InlineData(3, 3, 6, 4)]
        public void ExpectedDuration_Given_Estimates_Then_IsThePertMeanRoundedToTheNearestDayWithHalvesUp(
            int optimistic,
            int mostLikely,
            int pessimistic,
            int expected)
        {
            EstimateHelper.ExpectedDuration(optimistic, mostLikely, pessimistic).ShouldBe(expected);
        }

        [Fact]
        public void ExpectedDuration_Given_NoSpread_Then_IsTheMostLikelyDuration()
        {
            // The reason that a plan with no estimates is scheduled as it always was.
            for (int duration = 0; duration <= 400; duration++)
            {
                EstimateHelper.ExpectedDuration(duration, duration, duration).ShouldBe(duration);
            }
        }

        [Fact]
        public void ExpectedDuration_Given_AnyEstimatesInsideTheRule_Then_IsBetweenOptimisticAndPessimisticAndNeverFallsAsPessimisticRises()
        {
            int checkedCount = 0;

            for (int mostLikely = 0; mostLikely <= 20; mostLikely++)
            {
                for (int optimistic = 0; optimistic <= mostLikely; optimistic++)
                {
                    int previous = -1;
                    for (int pessimistic = mostLikely; pessimistic <= 30; pessimistic++)
                    {
                        int expected = EstimateHelper.ExpectedDuration(optimistic, mostLikely, pessimistic);

                        expected.ShouldBeGreaterThanOrEqualTo(optimistic);
                        expected.ShouldBeLessThanOrEqualTo(pessimistic);
                        expected.ShouldBeGreaterThanOrEqualTo(previous);
                        previous = expected;
                        checkedCount++;
                    }
                }
            }

            // Guards the guard.
            checkedCount.ShouldBeGreaterThan(3000);
        }

        [Theory]
        [InlineData(4, 12, 1.3333333333)]
        [InlineData(5, 5, 0.0)]
        [InlineData(0, 6, 1.0)]
        public void StandardDeviation_Given_OptimisticAndPessimistic_Then_IsASixthOfTheirDifference(
            int optimistic,
            int pessimistic,
            double expected)
        {
            EstimateHelper.StandardDeviation(optimistic, pessimistic).ShouldBe(expected, 1e-9);
        }

        [Theory]
        // Nothing of the activity's own: both follow ML.
        [InlineData(10, null, null, 10, 10)]
        // Values of the activity's own.
        [InlineData(10, 6, 14, 6, 14)]
        [InlineData(10, 6, null, 6, 10)]
        [InlineData(10, null, 14, 10, 14)]
        // Values outside the rule move to the nearest value inside it.
        [InlineData(10, 12, 5, 10, 10)]
        [InlineData(10, -3, 14, 0, 14)]
        // A milestone has no uncertainty, whatever it was given.
        [InlineData(0, 4, 9, 0, 0)]
        [InlineData(-5, 4, 9, 0, 0)]
        public void Effective_Given_MostLikelyAndTheActivitysOwnValues_Then_FollowsOrMovesInsideTheRule(
            int mostLikely,
            int? ownOptimistic,
            int? ownPessimistic,
            int expectedOptimistic,
            int expectedPessimistic)
        {
            (int optimistic, int ml, int pessimistic) = EstimateHelper.Effective(mostLikely, ownOptimistic, ownPessimistic);

            optimistic.ShouldBe(expectedOptimistic);
            ml.ShouldBe(Math.Max(0, mostLikely));
            pessimistic.ShouldBe(expectedPessimistic);
        }

        [Theory]
        // ML moves past an O that the user gave: O moves to ML.
        [InlineData(3, 6, 16, 3, 16)]
        [InlineData(8, 6, 16, 6, 16)]
        // ML moves past a P that the user gave: P moves to ML.
        [InlineData(20, 6, 16, 6, 20)]
        // ML stays inside the values that the user gave: they stay.
        [InlineData(10, 6, 16, 6, 16)]
        // Nothing that the user gave stays nothing.
        [InlineData(3, null, null, null, null)]
        [InlineData(20, null, 16, null, 20)]
        // ML is 0: what the user gave is 0 too.
        [InlineData(0, 6, 16, 0, 0)]
        [InlineData(0, null, 16, null, 0)]
        public void PullToMostLikely_Given_ANewMostLikely_Then_AValueThatItCrossedMovesToIt(
            int mostLikely,
            int? ownOptimistic,
            int? ownPessimistic,
            int? expectedOptimistic,
            int? expectedPessimistic)
        {
            (int? optimistic, int? pessimistic) = EstimateHelper.PullToMostLikely(mostLikely, ownOptimistic, ownPessimistic);

            optimistic.ShouldBe(expectedOptimistic);
            pessimistic.ShouldBe(expectedPessimistic);
        }

        #endregion

        #region An activity that has no estimates of its own

        [Fact]
        public void Activity_Given_ADurationAndNoEstimates_Then_ItIsAnActivityWithNoUncertainty()
        {
            using CoreViewModel core = CreateCore(new ActivityModel { Id = c_Id, Duration = 7 });
            IManagedActivityViewModel activity = core.RawActivities.Single();

            activity.Duration.ShouldBe(7);
            activity.MostLikelyDuration.ShouldBe(7);
            activity.OptimisticDuration.ShouldBe(7);
            activity.PessimisticDuration.ShouldBe(7);
            activity.OwnOptimisticDuration.ShouldBeNull();
            activity.OwnPessimisticDuration.ShouldBeNull();
            activity.HasOwnEstimates.ShouldBeFalse();
            activity.StandardDeviation.ShouldBe(0.0);
        }

        [Fact]
        public void MostLikelyDuration_WhenSet_Then_TheDurationAndTheOtherEstimatesFollowIt()
        {
            using CoreViewModel core = CreateCore(new ActivityModel { Id = c_Id, Duration = 7 });
            IManagedActivityViewModel activity = core.RawActivities.Single();

            activity.MostLikelyDuration = 12;

            activity.Duration.ShouldBe(12);
            activity.OptimisticDuration.ShouldBe(12);
            activity.PessimisticDuration.ShouldBe(12);
            activity.HasOwnEstimates.ShouldBeFalse();
        }

        [Theory]
        [InlineData(7, 0)]
        [InlineData(7, -4)]
        public void MostLikelyDuration_Given_ANegativeValue_Then_IsZero(int before, int value)
        {
            using CoreViewModel core = CreateCore(new ActivityModel { Id = c_Id, Duration = before });
            IManagedActivityViewModel activity = core.RawActivities.Single();

            activity.MostLikelyDuration = value;

            activity.MostLikelyDuration.ShouldBe(0);
            activity.Duration.ShouldBe(0);
            activity.IsDummy.ShouldBeTrue();
        }

        [Fact]
        public void Duration_WhenSet_Then_SetsTheMostLikelyDuration()
        {
            // What a caller that sets a duration means. The Gantt chart sets it when a user drags
            // the end of a bar.
            using CoreViewModel core = CreateCore(new ActivityModel { Id = c_Id, Duration = 7 });
            IManagedActivityViewModel activity = core.RawActivities.Single();

            activity.Duration = 9;

            activity.MostLikelyDuration.ShouldBe(9);
            activity.Duration.ShouldBe(9);
        }

        [Fact]
        public void SetActivityDuration_Given_AnActivityWithNoEstimates_Then_IsTheDurationOfTheActivity()
        {
            using CoreViewModel core = CreateCore(new ActivityModel { Id = c_Id, Duration = 7 });

            core.SetActivityDuration(c_Id, 4);

            IManagedActivityViewModel activity = core.RawActivities.Single();
            activity.Duration.ShouldBe(4);
            activity.MostLikelyDuration.ShouldBe(4);
        }

        #endregion

        #region An activity with estimates of its own

        [Fact]
        public void OptimisticDuration_WhenSet_Then_ItIsTheActivitysOwnAndTheExpectedDurationMoves()
        {
            using CoreViewModel core = CreateCore(new ActivityModel { Id = c_Id, Duration = 10 });
            IManagedActivityViewModel activity = core.RawActivities.Single();

            activity.OptimisticDuration = 4;

            // O = 4, ML = 10, P follows ML = 10: (4 + 40 + 10) / 6 = 9.
            activity.OwnOptimisticDuration.ShouldBe(4);
            activity.OwnPessimisticDuration.ShouldBeNull();
            activity.OptimisticDuration.ShouldBe(4);
            activity.PessimisticDuration.ShouldBe(10);
            activity.Duration.ShouldBe(9);
            activity.HasOwnEstimates.ShouldBeTrue();
            activity.StandardDeviation.ShouldBe(1.0, 1e-9);
        }

        [Fact]
        public void PessimisticDuration_WhenSet_Then_ItIsTheActivitysOwnAndTheExpectedDurationMoves()
        {
            using CoreViewModel core = CreateCore(new ActivityModel { Id = c_Id, Duration = 10 });
            IManagedActivityViewModel activity = core.RawActivities.Single();

            activity.PessimisticDuration = 22;

            // O follows ML = 10, ML = 10, P = 22: (10 + 40 + 22) / 6 = 12.
            activity.OwnOptimisticDuration.ShouldBeNull();
            activity.OwnPessimisticDuration.ShouldBe(22);
            activity.Duration.ShouldBe(12);
        }

        [Fact]
        public void MostLikelyDuration_Given_ValuesOfTheActivitysOwn_Then_ThoseThatFollowItMoveWithItAndThoseOfItsOwnStay()
        {
            using CoreViewModel core = CreateCore(new ActivityModel { Id = c_Id, Duration = 10 });
            IManagedActivityViewModel activity = core.RawActivities.Single();
            activity.OptimisticDuration = 4;

            activity.MostLikelyDuration = 14;

            // O stays at its own 4, and P follows ML to 14: (4 + 56 + 14) / 6 = 12.33, which is 12.
            activity.OptimisticDuration.ShouldBe(4);
            activity.PessimisticDuration.ShouldBe(14);
            activity.OwnPessimisticDuration.ShouldBeNull();
            activity.Duration.ShouldBe(12);
        }

        [Fact]
        public void MostLikelyDuration_Given_AValueThatCrossesAnEstimateTheUserGave_Then_ThatEstimateMovesToIt()
        {
            using CoreViewModel core = CreateCore(new ActivityModel
            {
                Id = c_Id,
                Duration = 10,
                MostLikelyDuration = 10,
                OptimisticDuration = 4,
                PessimisticDuration = 16,
            });
            IManagedActivityViewModel activity = core.RawActivities.Single();

            // Below the O that the user gave: O moves down to ML. P is untouched.
            activity.MostLikelyDuration = 3;
            activity.OwnOptimisticDuration.ShouldBe(3);
            activity.OwnPessimisticDuration.ShouldBe(16);

            // Above the P that the user gave: P moves up to ML. O is not moved back up: it is
            // still the value that ML moved it to, which is inside the rule.
            activity.MostLikelyDuration = 20;
            activity.OwnPessimisticDuration.ShouldBe(20);
            activity.OwnOptimisticDuration.ShouldBe(3);
            activity.OptimisticDuration.ShouldBe(3);
            activity.Duration.ShouldBe(EstimateHelper.ExpectedDuration(3, 20, 20));
        }

        [Fact]
        public void MostLikelyDuration_Given_Zero_Then_TheEstimatesTheUserGaveAreZeroToo()
        {
            using CoreViewModel core = CreateCore(new ActivityModel
            {
                Id = c_Id,
                Duration = 10,
                MostLikelyDuration = 10,
                OptimisticDuration = 4,
                PessimisticDuration = 16,
            });
            IManagedActivityViewModel activity = core.RawActivities.Single();

            activity.MostLikelyDuration = 0;

            activity.OwnOptimisticDuration.ShouldBe(0);
            activity.OwnPessimisticDuration.ShouldBe(0);
            activity.Duration.ShouldBe(0);
            activity.IsDummy.ShouldBeTrue();
        }

        [Fact]
        public void ResetEstimates_Then_OptimisticAndPessimisticFollowMostLikelyAgain()
        {
            using CoreViewModel core = CreateCore(new ActivityModel
            {
                Id = c_Id,
                Duration = 10,
                MostLikelyDuration = 10,
                OptimisticDuration = 4,
                PessimisticDuration = 16,
            });
            IManagedActivityViewModel activity = core.RawActivities.Single();
            activity.OptimisticDuration = 99; // An error to clear: it is above ML.

            activity.ResetEstimates();

            activity.OwnOptimisticDuration.ShouldBeNull();
            activity.OwnPessimisticDuration.ShouldBeNull();
            activity.OptimisticDuration.ShouldBe(10);
            activity.PessimisticDuration.ShouldBe(10);
            activity.Duration.ShouldBe(10);
            activity.HasOwnEstimates.ShouldBeFalse();
            ErrorsOf(activity, nameof(IManagedActivityViewModel.OptimisticDuration)).ShouldBeEmpty();
            ErrorsOf(activity, nameof(IManagedActivityViewModel.PessimisticDuration)).ShouldBeEmpty();
        }

        [Fact]
        public void ResetActivityEstimates_Given_SomeActivities_Then_OnlyThoseActivitiesFollowMostLikelyAgain()
        {
            using CoreViewModel core = CreateCore(
                new ActivityModel { Id = 1, Duration = 9, MostLikelyDuration = 10, OptimisticDuration = 2, PessimisticDuration = 12 },
                new ActivityModel { Id = 2, Duration = 9, MostLikelyDuration = 10, OptimisticDuration = 2, PessimisticDuration = 12 },
                new ActivityModel { Id = 3, Duration = 5 });
            core.IsProjectScenarioUpdated = false;

            core.ResetActivityEstimates([1, 3]);

            IManagedActivityViewModel first = core.RawActivities.Single(x => x.Id == 1);
            IManagedActivityViewModel second = core.RawActivities.Single(x => x.Id == 2);
            first.HasOwnEstimates.ShouldBeFalse();
            first.Duration.ShouldBe(10);

            // Not selected, so not touched.
            second.OwnOptimisticDuration.ShouldBe(2);
            second.OwnPessimisticDuration.ShouldBe(12);
            second.Duration.ShouldBe(9);

            core.IsProjectScenarioUpdated.ShouldBeTrue();
        }

        [Fact]
        public void ResetActivityEstimates_Given_ActivitiesThatHaveNoEstimatesOfTheirOwn_Then_NothingChangesAndThePlanIsNotMarkedAsUpdated()
        {
            using CoreViewModel core = CreateCore(new ActivityModel { Id = 1, Duration = 5 });
            core.IsProjectScenarioUpdated = false;

            core.ResetActivityEstimates([1]);

            core.IsProjectScenarioUpdated.ShouldBeFalse();
            core.RawActivities.Single().Duration.ShouldBe(5);
        }

        [Fact]
        public async Task ResetEstimatesCommand_Given_ASelection_Then_ResetsTheSelectedActivities()
        {
            using CoreViewModel core = CreateCore(
                new ActivityModel { Id = 1, Duration = 9, MostLikelyDuration = 10, OptimisticDuration = 2, PessimisticDuration = 12 },
                new ActivityModel { Id = 2, Duration = 9, MostLikelyDuration = 10, OptimisticDuration = 2, PessimisticDuration = 12 });
            var manager = new ActivitiesManagerViewModel(core, CoreViewModelFixture.CreateDialogService());
            manager.SelectedActivities[1] = core.RawActivities.Single(x => x.Id == 1);
            manager.HasSelectedActivities = true;

            manager.ResetEstimatesCommand.Execute(null);
            await WaitUntilAsync(() => !core.RawActivities.Single(x => x.Id == 1).HasOwnEstimates);

            core.RawActivities.Single(x => x.Id == 1).HasOwnEstimates.ShouldBeFalse();
            core.RawActivities.Single(x => x.Id == 2).HasOwnEstimates.ShouldBeTrue();
        }

        [Fact]
        public void ResetEstimatesCommand_Given_NoSelection_Then_CannotExecute()
        {
            using CoreViewModel core = CreateCore(new ActivityModel { Id = 1, Duration = 5 });
            var manager = new ActivitiesManagerViewModel(core, CoreViewModelFixture.CreateDialogService());

            manager.ResetEstimatesCommand.CanExecute(null).ShouldBeFalse();

            manager.HasSelectedActivities = true;

            manager.ResetEstimatesCommand.CanExecute(null).ShouldBeTrue();
        }

        #endregion

        #region Validation

        [Theory]
        [InlineData(15)]
        [InlineData(-3)]
        public void OptimisticDuration_Given_AValueOutsideTheRule_Then_IsLimitedToItAndTheActivityReportsAnError(int value)
        {
            using CoreViewModel core = CreateCore(new ActivityModel { Id = c_Id, Duration = 10 });
            IManagedActivityViewModel activity = core.RawActivities.Single();

            activity.OptimisticDuration = value;

            activity.OptimisticDuration.ShouldBe(Math.Clamp(value, 0, 10));
            ErrorsOf(activity, nameof(IManagedActivityViewModel.OptimisticDuration))
                .ShouldBe([Resource.ProjectPlan.Labels.Label_OptimisticMustBeBetweenZeroAndMostLikely]);

            // A valid value takes the error away.
            activity.OptimisticDuration = 5;

            activity.OptimisticDuration.ShouldBe(5);
            ErrorsOf(activity, nameof(IManagedActivityViewModel.OptimisticDuration)).ShouldBeEmpty();
        }

        [Fact]
        public void PessimisticDuration_Given_AValueBelowMostLikely_Then_IsLimitedToItAndTheActivityReportsAnError()
        {
            using CoreViewModel core = CreateCore(new ActivityModel { Id = c_Id, Duration = 10 });
            IManagedActivityViewModel activity = core.RawActivities.Single();

            activity.PessimisticDuration = 6;

            activity.PessimisticDuration.ShouldBe(10);
            activity.OwnPessimisticDuration.ShouldBe(10);
            ErrorsOf(activity, nameof(IManagedActivityViewModel.PessimisticDuration))
                .ShouldBe([Resource.ProjectPlan.Labels.Label_PessimisticMustBeGreaterThanOrEqualToMostLikely]);

            activity.PessimisticDuration = 12;

            activity.PessimisticDuration.ShouldBe(12);
            ErrorsOf(activity, nameof(IManagedActivityViewModel.PessimisticDuration)).ShouldBeEmpty();
        }

        [Fact]
        public void OptimisticAndPessimisticDuration_Given_AMilestone_Then_MustBeZero()
        {
            using CoreViewModel core = CreateCore(new ActivityModel { Id = c_Id, Duration = 0 });
            IManagedActivityViewModel activity = core.RawActivities.Single();

            activity.PessimisticDuration = 5;

            activity.PessimisticDuration.ShouldBe(0);
            activity.Duration.ShouldBe(0);
            ErrorsOf(activity, nameof(IManagedActivityViewModel.PessimisticDuration))
                .ShouldBe([Resource.ProjectPlan.Labels.Label_OptimisticAndPessimisticMustBeZeroWhenMostLikelyIsZero]);
        }

        [Fact]
        public void MostLikelyDuration_Given_ALongerDurationThanTheConstraintsAllow_Then_ReportsTheErrorOnMostLikelyAndOnDuration()
        {
            // The constraint is checked against the duration that will be scheduled, which is
            // the expected duration, and the user is editing the most likely one.
            using CoreViewModel core = CreateCore(new ActivityModel
            {
                Id = c_Id,
                Duration = 2,
                MinimumEarliestStartTime = 3,
                MaximumLatestFinishTime = 8,
            });
            IManagedActivityViewModel activity = core.RawActivities.Single();

            activity.MostLikelyDuration = 6;

            ErrorsOf(activity, nameof(IManagedActivityViewModel.MostLikelyDuration)).ShouldNotBeEmpty();
            ErrorsOf(activity, nameof(IManagedActivityViewModel.Duration)).ShouldNotBeEmpty();

            activity.MostLikelyDuration = 5;

            ErrorsOf(activity, nameof(IManagedActivityViewModel.MostLikelyDuration)).ShouldBeEmpty();
            ErrorsOf(activity, nameof(IManagedActivityViewModel.Duration)).ShouldBeEmpty();
        }

        #endregion

        #region An activity that is made from a model

        [Fact]
        public void Activity_Given_AModelWhoseDurationDisagreesWithItsEstimates_Then_TheEstimatesWin()
        {
            // A file that somebody edited by hand, or an import that set both.
            using CoreViewModel core = CreateCore(new ActivityModel
            {
                Id = c_Id,
                Duration = 99,
                MostLikelyDuration = 10,
                OptimisticDuration = 4,
                PessimisticDuration = 16,
            });
            IManagedActivityViewModel activity = core.RawActivities.Single();

            // (4 + 40 + 16) / 6 = 10.
            activity.Duration.ShouldBe(10);
            activity.MostLikelyDuration.ShouldBe(10);
        }

        [Fact]
        public void Activity_Given_AModelWithEstimatesOutsideTheRule_Then_TheyAreMovedInside()
        {
            using CoreViewModel core = CreateCore(new ActivityModel
            {
                Id = c_Id,
                Duration = 10,
                MostLikelyDuration = 10,
                OptimisticDuration = 14,
                PessimisticDuration = 3,
            });
            IManagedActivityViewModel activity = core.RawActivities.Single();

            activity.OwnOptimisticDuration.ShouldBe(10);
            activity.OwnPessimisticDuration.ShouldBe(10);
            activity.Duration.ShouldBe(10);
        }

        [Fact]
        public void Activity_Given_AModelWithAMostLikelyDurationAndNoDuration_Then_TheDurationIsWorkedOutFromTheEstimates()
        {
            using CoreViewModel core = CreateCore(new ActivityModel
            {
                Id = c_Id,
                MostLikelyDuration = 10,
                OptimisticDuration = 4,
            });
            IManagedActivityViewModel activity = core.RawActivities.Single();

            activity.Duration.ShouldBe(9);
        }

        #endregion

        #region Copies and announcements

        [Fact]
        public void DeepCopy_Given_AnActivityWithEstimates_Then_CarriesThemAsTheUserGaveThem()
        {
            using CoreViewModel core = CreateCore(
                new ActivityModel { Id = 1, Duration = 10, MostLikelyDuration = 10, OptimisticDuration = 4 },
                new ActivityModel { Id = 2, Duration = 6 });

            ActivityModel withEstimates = core.RawActivities.Single(x => x.Id == 1).DeepCopy().Activity;
            ActivityModel withNone = core.RawActivities.Single(x => x.Id == 2).DeepCopy().Activity;

            // The Duration is the scheduled one, and the estimates are those that the user gave.
            (withEstimates.Duration, withEstimates.MostLikelyDuration, withEstimates.OptimisticDuration, withEstimates.PessimisticDuration)
                .ShouldBe((9, 10, 4, (int?)null));
            (withNone.Duration, withNone.MostLikelyDuration, withNone.OptimisticDuration, withNone.PessimisticDuration)
                .ShouldBe((6, 6, (int?)null, (int?)null));
        }

        [Fact]
        public void CloneObject_Given_ADependentActivity_Then_KeepsTheEstimatesAndTheDuration()
        {
            // The activity that the compiler copies. A copy that dropped an estimate would be
            // the same fault one layer down from the mapping.
            var activity = new DependentActivity(c_Id, 9, canBeRemoved: false)
            {
                MostLikelyDuration = 10,
                OwnOptimisticDuration = 4,
                OwnPessimisticDuration = 16,
            };

            var clone = (DependentActivity)activity.CloneObject();

            (clone.Duration, clone.MostLikelyDuration, clone.OwnOptimisticDuration, clone.OwnPessimisticDuration)
                .ShouldBe((9, 10, 4, 16));
        }

        [Fact]
        public void DependentActivity_Given_OnlyADuration_Then_ItsMostLikelyDurationIsThatDuration()
        {
            new DependentActivity(c_Id, 6, canBeRemoved: false).MostLikelyDuration.ShouldBe(6);
        }

        [Fact]
        public void CloneObject_Given_AnActivityWithEstimates_Then_TheCopyForTheCompilerHasTheExpectedDuration()
        {
            using CoreViewModel core = CreateCore(
                new ActivityModel { Id = c_Id, Duration = 10, MostLikelyDuration = 10, OptimisticDuration = 4, PessimisticDuration = 10 });
            IManagedActivityViewModel activity = core.RawActivities.Single();

            var copy = (IDependentActivity)activity.CloneObject();

            copy.Duration.ShouldBe(9);
            copy.MostLikelyDuration.ShouldBe(10);
            copy.OwnOptimisticDuration.ShouldBe(4);
            copy.OwnPessimisticDuration.ShouldBe(10);

            // And it is a copy: editing the activity does not move it.
            activity.MostLikelyDuration = 30;
            copy.MostLikelyDuration.ShouldBe(10);
        }

        [Fact]
        public void EstimateSetters_WhenCalled_Then_AnnounceTheDurationAndWhatFollowsFromIt()
        {
            using CoreViewModel core = CreateCore(new ActivityModel { Id = c_Id, Duration = 10 });
            IManagedActivityViewModel activity = core.RawActivities.Single();
            var announced = new List<string?>();
            activity.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

            activity.OptimisticDuration = 4;

            announced.ShouldContain(nameof(IManagedActivityViewModel.OptimisticDuration));
            announced.ShouldContain(nameof(IManagedActivityViewModel.OwnOptimisticDuration));
            announced.ShouldContain(nameof(IManagedActivityViewModel.StandardDeviation));
            announced.ShouldContain(nameof(IManagedActivityViewModel.HasOwnEstimates));
            announced.ShouldContain(nameof(IManagedActivityViewModel.Duration));
            announced.ShouldContain(nameof(IManagedActivityViewModel.TotalSlack));
        }

        #endregion

        #region The compiler

        [Fact]
        public void Compile_Given_AnActivityWithEstimates_Then_ItIsScheduledForItsExpectedDuration()
        {
            // A is 4 to 10 days, most often 10, and so expected at (4 + 40 + 10) / 6 = 9. B follows
            // it, and takes 5. The network takes 14 days, and not the 15 that the most likely
            // durations give.
            using CoreViewModel core = CreateCore(
                [
                    new DependentActivityModel
                    {
                        Activity = new ActivityModel { Id = 1, DisplayOrder = 1, Duration = 10, MostLikelyDuration = 10, OptimisticDuration = 4 },
                    },
                    new DependentActivityModel
                    {
                        Activity = new ActivityModel { Id = 2, DisplayOrder = 2, Duration = 5 },
                        Dependencies = [1],
                    },
                ]);

            core.RunCompile();
            core.BuildNetworkMetrics();

            core.RawActivities.Single(x => x.Id == 1).Duration.ShouldBe(9);
            core.NetworkMetrics.Duration.ShouldBe(14);
        }

        [Fact]
        public void Compile_Given_ActivitiesWithNoEstimates_Then_TheNetworkIsWhatItAlwaysWas()
        {
            using CoreViewModel core = CreateCore(
                [
                    new DependentActivityModel { Activity = new ActivityModel { Id = 1, DisplayOrder = 1, Duration = 10 } },
                    new DependentActivityModel { Activity = new ActivityModel { Id = 2, DisplayOrder = 2, Duration = 5 }, Dependencies = [1] },
                ]);

            core.RunCompile();
            core.BuildNetworkMetrics();

            core.NetworkMetrics.Duration.ShouldBe(15);
        }

        #endregion

        #region The mapping

        [Fact]
        public void ToDependentActivity_Given_AModelWithNoMostLikelyDuration_Then_ItIsTheDurationAndNeverZero()
        {
            // The model of a file from before the estimates, of an import, and of any caller that sets
            // only the Duration. This is the one place where a missing member must not become 0.
            var mapper = new ProjectPlanMapper();
            var model = new ActivityModel { Id = c_Id, Duration = 7 };

            DependentActivity fromActivityModel = mapper.ToDependentActivity(model);
            DependentActivity fromDependentModel = mapper.ToDependentActivity(new DependentActivityModel { Activity = model });

            foreach (DependentActivity activity in new[] { fromActivityModel, fromDependentModel })
            {
                activity.Duration.ShouldBe(7);
                activity.MostLikelyDuration.ShouldBe(7);
                activity.OwnOptimisticDuration.ShouldBeNull();
                activity.OwnPessimisticDuration.ShouldBeNull();
            }
        }

        [Fact]
        public void ActivityEstimates_RoundTrippedThroughTheModel_Then_AreTheActivitys()
        {
            var mapper = new ProjectPlanMapper();
            var model = new ActivityModel
            {
                Id = c_Id,
                Duration = 9,
                MostLikelyDuration = 10,
                OptimisticDuration = 4,
                PessimisticDuration = 16,
            };

            DependentActivity activity = mapper.ToDependentActivity(new DependentActivityModel { Activity = model });
            ActivityModel back = mapper.ToDependentActivityModel(activity).Activity;

            (activity.MostLikelyDuration, activity.OwnOptimisticDuration, activity.OwnPessimisticDuration).ShouldBe((10, 4, 16));
            (back.Duration, back.MostLikelyDuration, back.OptimisticDuration, back.PessimisticDuration).ShouldBe((9, 10, 4, 16));

            // The other direction of the model to an activity, which the graphs use.
            DependentActivity fromActivityModel = mapper.ToDependentActivity(model);
            (fromActivityModel.MostLikelyDuration, fromActivityModel.OwnOptimisticDuration, fromActivityModel.OwnPessimisticDuration)
                .ShouldBe((10, 4, 16));
        }

        [Fact]
        public void ToDependentActivityModel_Given_AManagedActivity_Then_CarriesTheEstimatesTheUserGave()
        {
            using CoreViewModel core = CreateCore(new ActivityModel { Id = c_Id, Duration = 10 });
            var activity = (ManagedActivityViewModel)core.RawActivities.Single();
            activity.PessimisticDuration = 22;
            var mapper = new ProjectPlanMapper();

            ActivityModel model = mapper.ToDependentActivityModel(activity).Activity;

            // O follows ML, so it is not given. The model has the value that the user gave, which is
            // null, and not the value that the activity has, which is 10.
            (model.Duration, model.MostLikelyDuration, model.OptimisticDuration, model.PessimisticDuration)
                .ShouldBe((12, 10, (int?)null, 22));
        }

        #endregion

        #region Helpers

        private static CoreViewModel CreateCore(params ActivityModel[] activities) =>
            CreateCore([.. activities.Select(x => new DependentActivityModel { Activity = x })]);

        private static CoreViewModel CreateCore(IReadOnlyList<DependentActivityModel> activities)
        {
            CoreViewModel core = CoreViewModelFixture.Create();
            core.ProjectStart = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
            core.GraphSettings = CoreViewModelFixture.DefaultGraphSettings;
            core.AddManagedActivities(activities);
            return core;
        }

        // The command runs its work on the task pool, so the effect arrives a moment after Execute returns.
        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            DateTime limit = DateTime.UtcNow.AddSeconds(30);
            while (!condition())
            {
                if (DateTime.UtcNow > limit)
                {
                    throw new TimeoutException(@"The command did not finish.");
                }
                await Task.Delay(10);
            }
        }

        private static List<string> ErrorsOf(IManagedActivityViewModel activity, string propertyName) =>
            [.. ((INotifyDataErrorInfo)activity).GetErrors(propertyName).Cast<string>()];

        #endregion
    }
}
