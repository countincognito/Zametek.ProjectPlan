using Shouldly;
using System;
using System.ComponentModel;
using System.Linq;
using Xunit;
using Zametek.Contract.ProjectPlan;
using static Zametek.ViewModel.ProjectPlan.Tests.SequencerScopes;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The resources a resource settings manager makes are started with it - those it made before and those it makes
    /// after, whether the user adds one or a plan is opened into it - and each resource starts the tracker set that it
    /// makes, and kills it.
    /// </summary>
    /// <remarks>
    /// The pumps stand in for the thread pool and the main thread, so each test says when a delivery runs. A tracker set
    /// refreshes what its day cells show when its pipeline delivers, which is what is counted.
    /// </remarks>
    public class ResourceSettingsManagerStartTests
    {
        [Fact]
        public void ProcessProjectScenario_Given_AStartedManager_Then_TheResourcesItMakesAreStarted()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Manager.StartSubscriptions();
            Drain(main, pool);

            harness.Core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(4), Guid.NewGuid(), @"Test");
            Drain(main, pool);
            RefreshCounter[] counters = harness.CountRefreshes();
            harness.Core.TrackerIndex = 3;
            Drain(main, pool);

            // The sample scenario has two resources.
            counters.Length.ShouldBe(2);
            counters.ShouldAllBe(x => x.Count > 0, "a resource's tracker set did not refresh its days");
        }

        [Fact]
        public void AddManagedResourceCommand_Given_AStartedManager_Then_TheResourceItMakesIsStarted()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Manager.StartSubscriptions();
            Drain(main, pool);

            harness.AddResource();
            Drain(main, pool);
            RefreshCounter[] counters = harness.CountRefreshes();
            harness.Core.TrackerIndex = 3;
            Drain(main, pool);

            counters.Length.ShouldBe(1);
            counters.ShouldAllBe(x => x.Count > 0, "the tracker set of a resource added to a started manager did not refresh its days");
        }

        [Fact]
        public void AddManagedResourceCommand_Given_AManagerNotStarted_Then_TheResourceItMakesIsNot()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();

            harness.AddResource();
            Drain(main, pool);
            RefreshCounter[] counters = harness.CountRefreshes();
            harness.Core.TrackerIndex = 3;
            Drain(main, pool);

            counters.Length.ShouldBe(1);
            counters.ShouldAllBe(x => x.Count == 0, "the tracker set of a resource of a manager that is not started refreshed its days");
        }

        [Fact]
        public void StartSubscriptions_Given_AResourceMadeBeforeIt_Then_StartsItWithIt()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.AddResource();
            Drain(main, pool);
            RefreshCounter[] counters = harness.CountRefreshes();

            harness.Manager.StartSubscriptions();
            Drain(main, pool);

            counters.Length.ShouldBe(1);
            counters.ShouldAllBe(x => x.Count > 0, "the tracker set of a resource made before the start did not refresh its days");
        }

        [Fact]
        public void KillSubscriptions_Given_AStartedResource_Then_ItsTrackerSetDeliversNothingMore()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Manager.StartSubscriptions();
            harness.AddResource();
            Drain(main, pool);
            RefreshCounter counter = harness.CountRefreshes().ShouldHaveSingleItem();
            harness.Core.TrackerIndex = 2;
            Drain(main, pool);
            counter.Count.ShouldBeGreaterThan(0, "the tracker set was not refreshing its days to begin with");
            counter.Reset();

            harness.Manager.RawResources.ShouldHaveSingleItem().KillSubscriptions();
            harness.Core.TrackerIndex = 3;
            Drain(main, pool);

            counter.Count.ShouldBe(0);
        }

        // Counts the times a tracker set has refreshed what its day cells show.
        private sealed class RefreshCounter
        {
            public RefreshCounter(IResourceTrackerSetViewModel trackerSet)
            {
                ((INotifyPropertyChanged)trackerSet).PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(IResourceTrackerSetViewModel.SearchSymbol))
                    {
                        Count++;
                    }
                };
            }

            public int Count { get; private set; }

            public void Reset() => Count = 0;
        }

        // A resource settings manager over a core, with the services a test never reaches stood in for.
        private sealed class Harness
            : IDisposable
        {
            private Harness(CoreViewModel core, ResourceSettingsManagerViewModel manager)
            {
                Core = core;
                Manager = manager;
            }

            public CoreViewModel Core { get; }

            public ResourceSettingsManagerViewModel Manager { get; }

            public static Harness Make()
            {
                (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();

                return new Harness(
                    core,
                    new ResourceSettingsManagerViewModel(core, settings, CoreViewModelFixture.CreateDialogService()));
            }

            // As the user does, with the button.
            public void AddResource()
            {
                Manager.AddManagedResourceCommand.Execute(null);
            }

            // A counter on the tracker set of each resource the manager holds.
            public RefreshCounter[] CountRefreshes()
            {
                return [.. Manager.RawResources.Select(x => new RefreshCounter(x.TrackerSet))];
            }

            public void Dispose()
            {
                Manager.Dispose();
                Core.Dispose();
            }
        }
    }
}
