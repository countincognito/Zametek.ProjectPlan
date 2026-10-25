using Shouldly;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;
using static Zametek.ViewModel.ProjectPlan.Tests.SequencerScopes;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for what a compilation and its cascade do while they run, and for what happens to the edits that are made
    /// meanwhile (ARCHITECTURE section 7 rule 14).
    ///
    /// Neither holds the core lock while it runs. Each takes a copy of what it reads in one short step, works on the
    /// copy, and publishes in another short step. That leaves three things to get right, and they are what these tests
    /// pin: nothing that waits on the lock waits for a compilation, an edit made during a compilation is never lost
    /// and is compiled once, and the results of a compilation that is out of date are never published over those of
    /// one that is not.
    ///
    /// The lock used to do all three. A grid commit took it, and so it waited until the compilation had ended, which
    /// ordered the edit after the compilation's marking of every activity as compiled. A test of any of them has to
    /// be in the middle of a compilation, and so the compilation is held there (see GatedCoreViewModel): an edit made
    /// at a random moment almost never lands in the window.
    /// </summary>
    public class CoreViewModelOffLockTests
    {
        private static readonly TimeSpan s_Patience = TimeSpan.FromSeconds(10);

        private const int c_ActivityCount = 12;

        private static readonly JsonSerializerOptions s_DescribeOptions = new()
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        };

        private static GatedCoreViewModel CreateLoaded(int activityCount = c_ActivityCount, int durationOffset = 0)
        {
            (GatedCoreViewModel core, _) = CoreViewModelFixture.CreateGated();
            core.ProcessProjectScenario(
                CoreViewModelFixture.CreateProjectScenario(activityCount, durationOffset),
                Guid.NewGuid(),
                @"Test");
            return core;
        }

        // Commits an edit as the grid does.
        private static void CommitEdit(IManagedActivityViewModel activity, int addedDuration)
        {
            var editable = (IEditableObject)activity;
            editable.BeginEdit();
            activity.Duration += addedDuration;
            editable.EndEdit();
        }

        // On another thread, so that a call that waits for a lock fails the test instead of hanging it.
        private static Task RunAway(Action action) => Task.Run(action).WaitAsync(s_Patience);

        #region Compilation

        [Fact]
        public async Task RunCompile_Given_AHeldCompilation_Then_AnotherThreadCanChangeASetting()
        {
            using GatedCoreViewModel core = CreateLoaded();
            core.HoldNextCompilation();
            Task compile = Task.Run(core.RunCompile);

            try
            {
                core.WaitUntilHeld();

                // The compilation has taken its copy and let go of the lock. A setter takes that lock, and it used to
                // wait for as long as the compilation ran.
                await RunAway(() => core.ProjectStart = core.ProjectStart.AddDays(1));
            }
            finally
            {
                core.ReleaseHeldCompilation();
            }

            await compile.WaitAsync(s_Patience);

            core.HasStaleOutputs.ShouldBeTrue("the compilation did not see the new project start");
            core.IsBusy.ShouldBeFalse();
        }

        [Fact]
        public async Task RunCompile_Given_AnEditCommittedDuringIt_Then_TheResultsArePublishedAsStale()
        {
            using GatedCoreViewModel core = CreateLoaded();
            IManagedActivityViewModel edited = core.RawActivities.Single(x => x.Id == 3);
            int originalDuration = edited.Duration;

            core.HoldNextCompilation();
            Task compile = Task.Run(core.RunCompile);

            try
            {
                core.WaitUntilHeld();
                await RunAway(() => CommitEdit(edited, 7));
            }
            finally
            {
                core.ReleaseHeldCompilation();
            }

            await compile.WaitAsync(s_Patience);

            // The results are those of the plan as it was copied, and they say so.
            core.GraphCompilation.DependentActivities.Single(x => x.Id == 3).Duration.ShouldBe(originalDuration);
            core.HasStaleOutputs.ShouldBeTrue("the outputs claimed to match a plan that has been edited since");

            // Compiling again takes the edit into account, and clears the flag.
            core.RunCompile();

            core.GraphCompilation.DependentActivities.Single(x => x.Id == 3).Duration.ShouldBe(originalDuration + 7);
            core.HasStaleOutputs.ShouldBeFalse();
        }

        [Fact]
        public async Task RunAutoCompile_Given_EditsDuringACompilation_Then_OneFollowUpTakesThemAll()
        {
            using GatedCoreViewModel core = CreateLoaded();
            core.AutoCompile = true;
            IManagedActivityViewModel first = core.RawActivities.Single(x => x.Id == 3);
            IManagedActivityViewModel second = core.RawActivities.Single(x => x.Id == 8);
            int firstDuration = first.Duration;
            int secondDuration = second.Duration;
            int before = core.CompilationsStarted;

            core.HoldNextCompilation();
            Task runner = Task.Run(core.RunAutoCompile);

            try
            {
                core.WaitUntilHeld();

                // Three commits to two activities. None of them waits for the compilation, and none starts another.
                await RunAway(() => CommitEdit(first, 2));
                await RunAway(() => CommitEdit(second, 3));
                await RunAway(() => CommitEdit(first, 4));

                core.CompilationsStarted.ShouldBe(before + 1, "an edit started a second compilation beside the first");
            }
            finally
            {
                core.ReleaseHeldCompilation();
            }

            await runner.WaitAsync(s_Patience);

            core.CompilationsStarted.ShouldBe(before + 2, "the edits were not compiled together, once, after the first compilation");
            core.GraphCompilation.DependentActivities.Single(x => x.Id == 3).Duration.ShouldBe(firstDuration + 6);
            core.GraphCompilation.DependentActivities.Single(x => x.Id == 8).Duration.ShouldBe(secondDuration + 3);
            core.HasStaleOutputs.ShouldBeFalse();
            core.IsBusy.ShouldBeFalse();
        }

        [Fact]
        public async Task RunCompile_Given_AutoCompileOnAndAnEditDuringIt_Then_ItCompilesAgainBeforeItReturns()
        {
            // The same, with a compilation that was asked for by hand: it is not the one that makes the compilations
            // the edits ask for, but it leaves the plan as it found it, and the plan was edited meanwhile.
            using GatedCoreViewModel core = CreateLoaded();
            core.AutoCompile = true;
            IManagedActivityViewModel edited = core.RawActivities.Single(x => x.Id == 3);
            int originalDuration = edited.Duration;
            int before = core.CompilationsStarted;

            core.HoldNextCompilation();
            Task compile = Task.Run(core.RunCompile);

            try
            {
                core.WaitUntilHeld();
                await RunAway(() => CommitEdit(edited, 5));
            }
            finally
            {
                core.ReleaseHeldCompilation();
            }

            await compile.WaitAsync(s_Patience);

            core.CompilationsStarted.ShouldBe(before + 2);
            core.GraphCompilation.DependentActivities.Single(x => x.Id == 3).Duration.ShouldBe(originalDuration + 5);
            core.HasStaleOutputs.ShouldBeFalse();
        }

        [Fact]
        public async Task RunAutoCompile_Given_AnotherThreadAlreadyCompiling_Then_ReturnsAtOnceAndItsRequestIsCompiledAfter()
        {
            using GatedCoreViewModel core = CreateLoaded();
            core.AutoCompile = true;
            IManagedActivityViewModel edited = core.RawActivities.Single(x => x.Id == 3);
            int originalDuration = edited.Duration;
            int before = core.CompilationsStarted;

            core.HoldNextCompilation();
            Task runner = Task.Run(core.RunAutoCompile);

            try
            {
                core.WaitUntilHeld();

                // The edit comes first and the request after it, as a command does it.
                await RunAway(() => CommitEdit(edited, 9));
                await RunAway(core.RunAutoCompile);

                core.CompilationsStarted.ShouldBe(before + 1, "a second thread compiled beside the first");
            }
            finally
            {
                core.ReleaseHeldCompilation();
            }

            await runner.WaitAsync(s_Patience);

            core.CompilationsStarted.ShouldBe(before + 2);
            core.GraphCompilation.DependentActivities.Single(x => x.Id == 3).Duration.ShouldBe(originalDuration + 9);
        }

        [Fact]
        public async Task RunCompile_Given_AutoCompileOff_Then_AnEditDuringItIsNotCompiledAfter()
        {
            using GatedCoreViewModel core = CreateLoaded();
            core.AutoCompile.ShouldBeFalse();
            IManagedActivityViewModel edited = core.RawActivities.Single(x => x.Id == 3);
            int before = core.CompilationsStarted;

            core.HoldNextCompilation();
            Task compile = Task.Run(core.RunCompile);

            try
            {
                core.WaitUntilHeld();
                await RunAway(() => CommitEdit(edited, 5));
            }
            finally
            {
                core.ReleaseHeldCompilation();
            }

            await compile.WaitAsync(s_Patience);

            core.CompilationsStarted.ShouldBe(before + 1, "a compilation was made that nobody asked for");
            core.HasStaleOutputs.ShouldBeTrue();
        }

        [Fact]
        public async Task RunCompile_Given_AScenarioLoadedDuringIt_Then_TheResultsAreDropped()
        {
            using GatedCoreViewModel core = CreateLoaded(c_ActivityCount);
            core.HoldNextCompilation();
            Task stale = Task.Run(core.RunCompile);
            IGraphCompilation<int, int, int, IDependentActivity> loaded;

            try
            {
                core.WaitUntilHeld();

                // The load holds the core lock for the whole of it and compiles under it. It must not wait for the
                // compilation that is held, which is waiting to publish into the scenario it is replacing.
                ProjectScenarioModel other = CoreViewModelFixture.CreateProjectScenario(5, durationOffset: 40);
                await RunAway(() => core.ProcessProjectScenario(other, Guid.NewGuid(), @"Other"));

                loaded = core.GraphCompilation;
            }
            finally
            {
                core.ReleaseHeldCompilation();
            }

            await stale.WaitAsync(s_Patience);

            core.GraphCompilation.ShouldBeSameAs(loaded);
            core.RawActivities.Count.ShouldBe(5);

            // Every activity still holds what the load's compilation gave it. The ids of the two scenarios overlap, and
            // the durations differ, so the results of the compilation that was dropped would show.
            foreach (IManagedActivityViewModel activity in core.RawActivities)
            {
                IDependentActivity compiled = loaded.DependentActivities.Single(x => x.Id == activity.Id);
                activity.EarliestStartTime.ShouldBe(compiled.EarliestStartTime, $@"activity {activity.Id} took results from a scenario that was gone");
            }
        }

        [Fact]
        public async Task RunCompile_Given_AResetDuringIt_Then_TheResultsAreDropped()
        {
            // A project that closes resets the scenario and compiles nothing, and so no newer compilation is there to
            // outrank the one that is running. Only the scenario can tell that its results belong to a plan that is gone.
            using GatedCoreViewModel core = CreateLoaded();

            core.HoldNextCompilation();
            Task stale = Task.Run(core.RunCompile);

            try
            {
                core.WaitUntilHeld();
                await RunAway(core.ResetProjectScenario);
            }
            finally
            {
                core.ReleaseHeldCompilation();
            }

            await stale.WaitAsync(s_Patience);

            core.RawActivities.ShouldBeEmpty();
            core.GraphCompilation.DependentActivities.ShouldBeEmpty("the results of a plan that was gone were published into an empty scenario");
        }

        [Fact]
        public async Task RunCompile_Given_ANewerCompilationPublishedFirst_Then_TheOlderResultsAreDropped()
        {
            using GatedCoreViewModel core = CreateLoaded();
            IManagedActivityViewModel edited = core.RawActivities.Single(x => x.Id == 3);

            core.HoldNextCompilation();
            Task older = Task.Run(core.RunCompile);
            IGraphCompilation<int, int, int, IDependentActivity> newer;

            try
            {
                core.WaitUntilHeld();

                // A compilation that starts later, from a plan that has been edited, passes the one that is held.
                await RunAway(() => CommitEdit(edited, 11));
                await RunAway(core.RunCompile);

                newer = core.GraphCompilation;
            }
            finally
            {
                core.ReleaseHeldCompilation();
            }

            await older.WaitAsync(s_Patience);

            core.GraphCompilation.ShouldBeSameAs(newer, "an older compilation was published over a newer one");
            core.HasStaleOutputs.ShouldBeFalse("the older compilation's publication changed what the newer one said");
        }

        [Fact]
        public async Task RunCompile_Given_AnActivityReplacedDuringIt_Then_TheNewOneGetsNothingOfTheOld()
        {
            using GatedCoreViewModel core = CreateLoaded();
            int lastId = core.RawActivities.Max(x => x.Id);

            core.HoldNextCompilation();
            Task compile = Task.Run(core.RunCompile);
            int newId;

            try
            {
                core.WaitUntilHeld();

                // The last activity goes, and the one that takes its place has the same id.
                core.RemoveManagedActivities([lastId]);
                newId = core.AddManagedActivity(displayOrder: 99);
            }
            finally
            {
                core.ReleaseHeldCompilation();
            }

            await compile.WaitAsync(s_Patience);

            newId.ShouldBe(lastId, "the test needs the new activity to take the id of the old one");

            // The compilation held the results of the old activity. They are not the new activity's.
            core.RawActivities.Single(x => x.Id == newId).EarliestStartTime.ShouldBeNull();
        }

        [Fact]
        public async Task ProcessProjectScenario_Given_AHeldCompilation_Then_AnotherThreadStillWaitsForTheLoad()
        {
            // A load rewrites the model, and nothing else should see the model part way, so it keeps the lock for the
            // whole of it, its compilation included. Only a compilation that is asked for on its own lets go.
            using GatedCoreViewModel core = CreateLoaded();
            core.HoldNextCompilation();
            ProjectScenarioModel other = CoreViewModelFixture.CreateProjectScenario(5, durationOffset: 2);
            Task load = Task.Run(() => core.ProcessProjectScenario(other, Guid.NewGuid(), @"Other"));
            Task setting;

            try
            {
                core.WaitUntilHeld();
                setting = Task.Run(() => core.ProjectStart = core.ProjectStart.AddDays(1));

                await Task.Delay(300);

                setting.IsCompleted.ShouldBeFalse("a setter got in part way through a load");
            }
            finally
            {
                core.ReleaseHeldCompilation();
            }

            await load.WaitAsync(s_Patience);
            await setting.WaitAsync(s_Patience);
        }

        [Fact]
        public async Task RunAutoCompile_Given_ARequestWithNoEditDuringACompilation_Then_ItIsCompiledAfter()
        {
            using GatedCoreViewModel core = CreateLoaded();
            core.AutoCompile = true;
            int before = core.CompilationsStarted;

            core.HoldNextCompilation();
            Task runner = Task.Run(core.RunAutoCompile);

            try
            {
                core.WaitUntilHeld();

                // A command that changed the plan some other way - the structure of it, say - asks for a compilation.
                // It has no edit to count, and so the request is the count.
                await RunAway(core.RunAutoCompile);
            }
            finally
            {
                core.ReleaseHeldCompilation();
            }

            await runner.WaitAsync(s_Patience);

            core.CompilationsStarted.ShouldBe(before + 2, "a request made during a compilation was not compiled");
        }

        [Fact]
        public async Task ProcessProjectScenario_Given_AnInputChangeDuringIt_Then_ItDoesNotCompileAgainUnderItsLock()
        {
            using GatedCoreViewModel core = CreateLoaded();
            core.AutoCompile = true;
            int before = core.CompilationsStarted;

            core.HoldNextCompilation();
            ProjectScenarioModel other = CoreViewModelFixture.CreateProjectScenario(5, durationOffset: 2);
            Task load = Task.Run(() => core.ProcessProjectScenario(other, Guid.NewGuid(), @"Other"));

            try
            {
                core.WaitUntilHeld();

                // Counting a change takes no lock, and so it gets through. The load holds the lock for the whole of it,
                // and it does not run the compilation that the change asks for under that hold.
                core.NoteInputsChanged();
            }
            finally
            {
                core.ReleaseHeldCompilation();
            }

            await load.WaitAsync(s_Patience);

            core.CompilationsStarted.ShouldBe(before + 1, "a load compiled twice under its lock");
        }

        [Fact]
        public void ResetProjectScenario_Given_PendingEdits_Then_NothingIsCompiledAfter()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            (GatedCoreViewModel core, GatedGraphCompilationService _) = CoreViewModelFixture.CreateGated();
            using GatedCoreViewModel owner = core;
            core.StartSubscriptions();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");
            Drain(main, pool);
            core.AutoCompile = true;
            int before = core.CompilationsStarted;

            // The edit counts, and queues the arming of a compilation. The reset comes before the arming is delivered.
            CommitEdit(core.RawActivities.Single(x => x.Id == 3), 5);
            core.ResetProjectScenario();
            Drain(main, pool);

            core.CompilationsStarted.ShouldBe(before, "an empty scenario was compiled");
        }

        [Fact]
        public void AutoCompile_Given_EditsMadeWhileItWasOff_Then_SwitchingItOnCompilesNothing()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            (GatedCoreViewModel core, GatedGraphCompilationService _) = CoreViewModelFixture.CreateGated();
            using GatedCoreViewModel owner = core;
            core.StartSubscriptions();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");
            Drain(main, pool);
            core.AutoCompile.ShouldBeFalse();
            int before = core.CompilationsStarted;

            CommitEdit(core.RawActivities.Single(x => x.Id == 3), 5);
            core.AutoCompile = true;
            Drain(main, pool);

            core.CompilationsStarted.ShouldBe(before, "switching automatic compilation on compiled the edits made without it");
        }

        #endregion

        #region Cascade

        [Fact]
        public async Task Cascade_Given_AHeldCascade_Then_AnotherThreadCanChangeASetting()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            (GatedCoreViewModel core, GatedGraphCompilationService graphs) = CoreViewModelFixture.CreateGated();
            using GatedCoreViewModel owner = core;
            core.StartSubscriptions();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");
            Drain(main, pool);

            int revision = core.CompilationOutputRevision;
            core.RunCompile();
            graphs.HoldNextCascade();
            Task cascade = Task.Run(pool.Pump.Drain);

            try
            {
                graphs.WaitUntilHeld();

                // The cascade has copied what it reads and let go of the lock. It is the longer of the two steps, and
                // the setter used to wait for all of it.
                await RunAway(() => core.ProjectStart = core.ProjectStart.AddDays(1));
            }
            finally
            {
                graphs.ReleaseHeldCascade();
            }

            await cascade.WaitAsync(s_Patience);

            core.CompilationOutputRevision.ShouldNotBe(revision, "the cascade did not publish");
        }

        [Fact]
        public async Task Cascade_Given_CompilationsPublishedDuringIt_Then_ItsOutputsAreDroppedAndOneCascadeFollows()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            (GatedCoreViewModel core, GatedGraphCompilationService graphs) = CoreViewModelFixture.CreateGated();
            using GatedCoreViewModel owner = core;
            core.StartSubscriptions();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");
            Drain(main, pool);

            IManagedActivityViewModel edited = core.RawActivities.Single(x => x.Id == 3);
            int revisionsPublished = 0;
            ((INotifyPropertyChanged)core).PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ICoreViewModel.CompilationOutputRevision))
                {
                    revisionsPublished++;
                }
            };
            int cascadesBefore = graphs.CascadesStarted;

            CommitEdit(edited, 4);
            core.RunCompile();
            graphs.HoldNextCascade();
            Task drain = Task.Run(pool.Pump.Drain);

            try
            {
                graphs.WaitUntilHeld();

                // Two more compilations publish while the first cascade is held. Each queues a cascade of its own.
                CommitEdit(edited, 4);
                core.RunCompile();
                CommitEdit(edited, 4);
                core.RunCompile();
            }
            finally
            {
                graphs.ReleaseHeldCascade();
            }

            await drain.WaitAsync(s_Patience);

            // The held cascade built from a compilation that was no longer current, and published nothing. One cascade
            // built from the newest, and the other queued ones found nothing to add.
            graphs.CascadesStarted.ShouldBe(cascadesBefore + 2);
            revisionsPublished.ShouldBe(1, "a cascade published outputs that were already out of date, or one was published twice");

            // The outputs are those of the newest compilation: building them again from it changes nothing.
            int duration = core.NetworkMetrics.Duration ?? -1;
            duration.ShouldBeGreaterThan(0);
            core.BuildNetworkMetrics();
            core.NetworkMetrics.Duration.ShouldBe(duration);
        }

        [Fact]
        public async Task Cascade_Given_AScenarioLoadedDuringIt_Then_ItsOutputsAreDropped()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            (GatedCoreViewModel core, GatedGraphCompilationService graphs) = CoreViewModelFixture.CreateGated();
            using GatedCoreViewModel owner = core;
            core.StartSubscriptions();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");
            Drain(main, pool);

            core.RunCompile();
            graphs.HoldNextCascade();
            Task drain = Task.Run(pool.Pump.Drain);
            Task load;

            try
            {
                graphs.WaitUntilHeld();

                // The load rebuilds every output itself, under the lock it holds, and it builds them one cascade at a
                // time: it waits for the cascade that is held, which is releasing nothing until the test says. So the
                // test lets the load get as far as replacing the scenario - which it does under the lock, before it
                // builds anything - and then releases the cascade. That cascade then finishes building, finds the lock
                // taken, and publishes only when the load is over.
                int scenarioRevision = core.ProjectScenarioRevision;
                ProjectScenarioModel other = CoreViewModelFixture.CreateProjectScenario(5, durationOffset: 40);
                load = Task.Run(() => core.ProcessProjectScenario(other, Guid.NewGuid(), @"Other"));

                SpinWait.SpinUntil(() => core.ProjectScenarioRevision != scenarioRevision, s_Patience)
                    .ShouldBeTrue("the load never replaced the scenario");
            }
            finally
            {
                graphs.ReleaseHeldCascade();
            }

            await load.WaitAsync(s_Patience);
            await drain.WaitAsync(s_Patience);
            Drain(main, pool);

            // What the load built is still there. The scenarios differ in length, and so do their outputs. (The metrics
            // are left out: a load puts back the ones its file holds, and this scenario has none.)
            string fromTheLoad = DescribeGraphs(core);

            core.BuildArrowGraph();
            core.BuildVertexGraph();
            core.BuildResourceSeriesSet();
            core.BuildTrackingSeriesSet();

            DescribeGraphs(core).ShouldBe(fromTheLoad, "the outputs of a scenario that was gone replaced those of the one that was loaded");
        }

        [Fact]
        public async Task Cascade_Given_AHeldCascade_Then_AnotherBuildOfTheColoursWaitsForIt()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            (GatedCoreViewModel core, GatedGraphCompilationService graphs) = CoreViewModelFixture.CreateGated();
            using GatedCoreViewModel owner = core;
            core.StartSubscriptions();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");
            Drain(main, pool);

            core.RunCompile();
            graphs.HoldNextCascade();
            Task cascade = Task.Run(pool.Pump.Drain);
            Task build;

            try
            {
                graphs.WaitUntilHeld();

                // The resource series and the tracking series draw from one sequence of colors. A build that draws while
                // the cascade is drawing would take colors out of its sequence.
                build = Task.Run(core.BuildResourceSeriesSet);

                await Task.Delay(300);

                build.IsCompleted.ShouldBeFalse("a build drew colors while a cascade was drawing them");
            }
            finally
            {
                graphs.ReleaseHeldCascade();
            }

            await cascade.WaitAsync(s_Patience);
            await build.WaitAsync(s_Patience);
        }

        [Fact]
        public async Task Cascade_Then_ItsOutputsAreTheOnesTheBuildMethodsGive()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            (GatedCoreViewModel core, GatedGraphCompilationService _) = CoreViewModelFixture.CreateGated();
            using GatedCoreViewModel owner = core;
            core.StartSubscriptions();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");

            // The cascade that the compilation starts, run on a thread of the pool and without the lock.
            core.RunCompile();
            await Task.Run(pool.Pump.Drain).WaitAsync(s_Patience);
            Drain(main, pool);

            string fromTheCascade = Describe(core);

            core.BuildArrowGraph();
            core.BuildVertexGraph();
            core.BuildResourceSeriesSet();
            core.BuildTrackingSeriesSet();
            core.BuildNetworkMetrics();
            core.BuildRiskMetrics();
            core.BuildFinancialMetrics();

            Describe(core).ShouldBe(fromTheCascade);
        }

        private static string DescribeGraphs(ICoreViewModel core) =>
            JsonSerializer.Serialize(
                new object?[]
                {
                    core.ArrowGraph,
                    core.VertexGraph,
                    core.ResourceSeriesSet,
                    core.TrackingSeriesSet,
                },
                s_DescribeOptions);

        // Every output of the cascade, as text, so that two sets of outputs can be compared by what is in them.
        private static string Describe(ICoreViewModel core) =>
            JsonSerializer.Serialize(
                new object?[]
                {
                    core.ArrowGraph,
                    core.VertexGraph,
                    core.ResourceSeriesSet,
                    core.TrackingSeriesSet,
                    core.Metrics,
                    core.ResourceMetrics,
                },
                s_DescribeOptions);

        #endregion
    }
}
