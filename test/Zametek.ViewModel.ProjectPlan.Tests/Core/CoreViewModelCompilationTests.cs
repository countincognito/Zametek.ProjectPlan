using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for the way a compilation reads and writes the plan.
    ///
    /// A compilation used to run directly on the live activity view models: it cloned
    /// them as it went and wrote its results back into them as it calculated. Anything
    /// editing an activity at the same time could therefore be caught mid-write, and a
    /// cloned collection could come out structurally broken - which is what starved the
    /// resource scheduler and hung the application (ARCHITECTURE section 7 rule 10).
    ///
    /// It now takes a copy of the plan, compiles that, and applies the results back
    /// afterwards, with both of those steps exclusive with the activities' own writes.
    /// These tests pin that: the results still land, an edit made during a compilation
    /// neither corrupts it nor is lost, and a compilation reads settings that have
    /// already been reconciled.
    /// </summary>
    public class CoreViewModelCompilationTests
    {
        [Fact]
        public void RunCompile_Then_PublishesResultsToTheActivities()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(12), Guid.NewGuid(), @"Test");

            core.HasCompilationErrors.ShouldBeFalse();

            // The activities are a chain of increasing duration, so a compiled plan has
            // every activity scheduled, and each one starting after the previous.
            IReadOnlyList<IManagedActivityViewModel> activities = core.RawActivities;
            activities.Count.ShouldBe(12);

            foreach (IManagedActivityViewModel activity in activities)
            {
                activity.EarliestStartTime.ShouldNotBeNull($@"activity {activity.Id} was not scheduled");
                activity.LatestFinishTime.ShouldNotBeNull($@"activity {activity.Id} was not scheduled");
                activity.AllocatedToResources.ShouldNotBeEmpty($@"activity {activity.Id} was not allocated");
            }

            foreach (IManagedActivityViewModel activity in activities.Where(x => x.Id > 1))
            {
                IManagedActivityViewModel predecessor = activities.Single(x => x.Id == activity.Id - 1);
                activity.EarliestStartTime.GetValueOrDefault()
                    .ShouldBeGreaterThanOrEqualTo(predecessor.EarliestFinishTime.GetValueOrDefault());
            }

            // The compiler derives the project's start and finish by reading them straight
            // off the activities it holds - which are these ones - and the network metrics
            // are built from that. So this only holds if the results were applied back to
            // the live activities rather than left behind on the copies that were compiled.
            // Rebuilt here because loading a scenario replaces the computed metrics with
            // the ones stored in the file, and this scenario carries none.
            int expectedDuration =
                activities.Max(x => x.LatestFinishTime.GetValueOrDefault())
                - activities.Min(x => x.EarliestStartTime.GetValueOrDefault());

            core.BuildNetworkMetrics();

            core.Metrics.Network.ShouldNotBeNull();
            core.Metrics.Network.Duration.ShouldBe(expectedDuration);
        }

        [Fact]
        public async Task RunCompile_Given_ConcurrentEdits_Then_CompilesAndKeepsTheEdits()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(25), Guid.NewGuid(), @"Test");

            IManagedActivityViewModel edited = core.RawActivities.Single(x => x.Id == 3);

            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var editorFaults = new List<Exception>();

            // Rewrites the target resources exactly as committing a grid edit does: the
            // set is cleared and refilled, which is the mutation that used to be able to
            // tear a clone in the middle of being taken.
            Task editor = Task.Run(() =>
            {
                try
                {
                    int counter = 0;

                    while (!stop.IsCancellationRequested)
                    {
                        edited.ResourceSelector.SetSelectedTargetResources([1 + (counter % 2)]);
                        ((System.ComponentModel.IEditableObject)edited).BeginEdit();
                        ((System.ComponentModel.IEditableObject)edited).EndEdit();
                        counter++;
                    }
                }
                catch (Exception ex)
                {
                    editorFaults.Add(ex);
                }
            });

            try
            {
                for (int i = 0; i < 40; i++)
                {
                    core.RunCompile();
                    core.HasCompilationErrors.ShouldBeFalse($@"compilation {i} reported errors");
                }
            }
            finally
            {
                await stop.CancelAsync();
                await editor.WaitAsync(TimeSpan.FromSeconds(10));
            }

            editorFaults.ShouldBeEmpty();

            // The edit is still whatever the editor last set, not something a compilation
            // overwrote: applying results must not write back the activity's own inputs.
            edited.TargetResources.Count.ShouldBe(1);
            edited.TargetResources.Single().ShouldBeOneOf(1, 2);

            // Every target set is still internally consistent. This is the exact property
            // that a torn clone violates: it enumerates and counts correctly while every
            // Contains misses, which is invisible to anything that only enumerates.
            foreach (IManagedActivityViewModel activity in core.RawActivities)
            {
                activity.TargetResources.All(activity.TargetResources.Contains).ShouldBeTrue(
                    $@"activity {activity.Id} has an inconsistent target resource set");
            }
        }

        /// <summary>
        /// A compilation's results have to be announced, not merely stored.
        ///
        /// The compiler used to hold the activity view models themselves as its graph, so
        /// calculating a time called the view model's setter, which announced it and
        /// everything derived from it. Compiling a copy of the plan and writing the results
        /// underneath those setters leaves every stored value correct while the grid keeps
        /// showing the previous compilation's numbers - and nothing that checks values
        /// notices, which is why this checks the announcements instead.
        /// </summary>
        [Fact]
        public void RunCompile_Then_AnnouncesTheCompiledValues()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(6), Guid.NewGuid(), @"Test");

            IManagedActivityViewModel activity = core.RawActivities.Single(x => x.Id == 2);

            var announced = new List<string>();
            activity.PropertyChanged += (_, e) => announced.Add(e.PropertyName ?? string.Empty);

            // Change a duration the way the grid does, then compile. Everything the grid
            // shows for this activity has to be announced as a result.
            activity.Duration += 3;
            announced.Clear();

            core.RunCompile();

            string[] expected =
            [
                nameof(IManagedActivityViewModel.EarliestStartTime),
                nameof(IManagedActivityViewModel.LatestFinishTime),
                nameof(IManagedActivityViewModel.FreeSlack),
                nameof(IManagedActivityViewModel.EarliestFinishTime),
                nameof(IManagedActivityViewModel.LatestStartTime),
                nameof(IManagedActivityViewModel.TotalSlack),
                nameof(IManagedActivityViewModel.InterferingSlack),
                nameof(IManagedActivityViewModel.IsCritical),
                nameof(IManagedActivityViewModel.EarliestStartDateTimeOffset),
                nameof(IManagedActivityViewModel.EarliestFinishDateTimeOffset),
                nameof(IManagedActivityViewModel.LatestStartDateTimeOffset),
                nameof(IManagedActivityViewModel.LatestFinishDateTimeOffset),
                nameof(IManagedActivityViewModel.ResourceDependenciesString),
                nameof(IManagedActivityViewModel.SuccessorsString),
            ];

            foreach (string property in expected)
            {
                announced.ShouldContain(property, $@"a compilation did not announce {property}");
            }
        }

        /// <summary>
        /// The sharp end of the race, exercised directly rather than through a whole
        /// compilation: copying an activity while its target resources are being rewritten.
        ///
        /// A compilation spends nearly all of its time compiling and only a moment copying,
        /// so driving this through RunCompile hardly ever lands in the window - the test
        /// above passes with or without the exclusion, which is why this one exists. Here
        /// the two operations are run against each other as fast as they will go, so the
        /// window is hit constantly.
        ///
        /// What is asserted is the exact property the corruption in the captured dump
        /// violated: a copied set that still enumerates and counts correctly while every
        /// Contains misses. Anything that only enumerates - saving, validating, the metrics
        /// - sees nothing wrong; the resource scheduler, which probes with Contains, starves.
        /// </summary>
        [Fact]
        public async Task CloneObject_Given_ConcurrentTargetEdits_Then_CopiesAreNeverTorn()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(4), Guid.NewGuid(), @"Test");

            IManagedActivityViewModel activity = core.RawActivities.Single(x => x.Id == 1);
            var editable = (System.ComponentModel.IEditableObject)activity;

            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var faults = new List<string>();

            // Rewrites the target set exactly as committing a grid edit does: cleared, then
            // refilled. Clearing zeroes the set's buckets before its entries, so a copy
            // taken in between carries the entries without the index that finds them.
            Task editor = Task.Run(() =>
            {
                var counter = 0;

                while (!stop.IsCancellationRequested)
                {
                    activity.ResourceSelector.SetSelectedTargetResources(
                        counter % 2 == 0 ? [1, 2] : [2]);
                    editable.BeginEdit();
                    editable.EndEdit();
                    counter++;
                }
            });

            try
            {
                for (int i = 0; i < 200_000 && !stop.IsCancellationRequested; i++)
                {
                    var copy = (IDependentActivity)activity.CloneObject();

                    if (!copy.TargetResources.All(copy.TargetResources.Contains))
                    {
                        faults.Add(
                            $@"copy {i} was torn: it holds [{string.Join(@",", copy.TargetResources)}] and counts {copy.TargetResources.Count}, but Contains finds none of them");
                        break;
                    }
                }
            }
            finally
            {
                await stop.CancelAsync();
                await editor.WaitAsync(TimeSpan.FromSeconds(10));
            }

            faults.ShouldBeEmpty();
        }

        [Fact]
        public void RunCompile_Given_APoisonedTargetSet_Then_DoesNotHang()
        {
            // The scheduler gates assignment on Contains, so the poisoned activity below
            // can never be placed. That used to spin forever; the watchdog bounds it.
            using CoreViewModel core = CoreViewModelFixture.Create(compilationTimeoutMilliseconds: 2_000);
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(8), Guid.NewGuid(), @"Test");

            IManagedActivityViewModel poisoned = core.RawActivities.Single(x => x.Id == 3);
            PoisonHashSet(poisoned.TargetResources);

            // The set now behaves the way the one in the captured dump did: it still holds
            // its entries and reports its count, but every Contains misses.
            poisoned.TargetResources.Count.ShouldBe(1);
            poisoned.TargetResources.All(poisoned.TargetResources.Contains).ShouldBeFalse();

            // It must come back - as a compilation error from the scheduler's own stall
            // detection, or as the watchdog cancelling it - either way without hanging.
            var completed = false;

            try
            {
                core.RunCompile();
                completed = true;
            }
            catch (GraphCompilationTimeoutException)
            {
                completed = true;
            }

            completed.ShouldBeTrue();
        }

        /// <summary>
        /// Reproduces the corruption seen in the captured dump: a set whose buckets have
        /// been zeroed while its entries remain, which is what the copy constructor
        /// produced when it copied a set that another thread was clearing.
        /// </summary>
        private static void PoisonHashSet(HashSet<int> set)
        {
            FieldInfo bucketsField = typeof(HashSet<int>)
                .GetField(@"_buckets", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(@"HashSet<int> no longer has a _buckets field.");

            var buckets = (int[]?)bucketsField.GetValue(set)
                ?? throw new InvalidOperationException(@"The set has no buckets to poison.");

            Array.Clear(buckets);
        }
    }
}
