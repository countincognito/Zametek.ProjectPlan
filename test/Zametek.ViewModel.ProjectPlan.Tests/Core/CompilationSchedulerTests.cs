using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for the rules that CompilationScheduler holds on its own, without a core (ARCHITECTURE section 7 rule 14): when
    /// the plan is due a compilation, in which order the snapshots may publish, which thread makes the compilations, and how
    /// a compilation is cut short.
    ///
    /// The scheduler has no lock and starts no thread, so each test is a plain sequence of calls, with two threads only
    /// where the rule is about two threads. The tests of the core go through the same rules, but they have to hold a whole
    /// compilation part way through to see them.
    /// </summary>
    public class CompilationSchedulerTests
    {
        private static readonly TimeSpan s_Patience = TimeSpan.FromSeconds(10);

        private static (List<IDependentActivity>, Dictionary<int, IManagedActivityViewModel>) NoCopies() =>
            (new List<IDependentActivity>(), new Dictionary<int, IManagedActivityViewModel>());

        private static readonly ConditionalWeakTable<CompilationScheduler, StrongBox<int>> s_Takes = new();

        // A snapshot of a plan with nothing in it: what matters here is the revision and the ticket.
        //
        // A compilation takes a snapshot, and with it the plan is not due again. A fault that leaves the plan due makes a run
        // compile for ever, and every compilation of these tests would take another snapshot. No test needs this many, and so
        // the helper stops the run, and the fault ends the test and does not make it loop.
        private static CompilationSnapshot Take(CompilationScheduler scheduler, int scenarioRevision = 0)
        {
            StrongBox<int> takes = s_Takes.GetOrCreateValue(scheduler);

            if (++takes.Value > 50)
            {
                throw new InvalidOperationException("more than fifty snapshots were taken: a run of compilations does not stop");
            }

            return scheduler.TakeSnapshot([], [], scenarioRevision, NoCopies);
        }

        // Two activities, the second after the first.
        private static List<IDependentActivity> TwoActivities()
        {
            var first = new DependentActivity(1, 3, canBeRemoved: false);
            var second = new DependentActivity(2, 4, canBeRemoved: false);
            second.Dependencies.Add(1);
            return [first, second];
        }

        #region Revisions

        [Fact]
        public void HasUnabsorbedInputs_Given_ANewScheduler_Then_False()
        {
            new CompilationScheduler().HasUnabsorbedInputs().ShouldBeFalse();
        }

        [Fact]
        public void NoteInputsChanged_Then_TheInputsAreUnabsorbed()
        {
            var scheduler = new CompilationScheduler();

            scheduler.NoteInputsChanged();

            scheduler.HasUnabsorbedInputs().ShouldBeTrue();
        }

        [Fact]
        public void AbsorbPendingInputs_Then_NothingIsPendingUntilTheNextChange()
        {
            var scheduler = new CompilationScheduler();
            scheduler.NoteInputsChanged();

            scheduler.AbsorbPendingInputs();

            scheduler.HasUnabsorbedInputs().ShouldBeFalse();

            scheduler.NoteInputsChanged();

            scheduler.HasUnabsorbedInputs().ShouldBeTrue();
        }

        [Fact]
        public void TakeSnapshot_Then_AbsorbsTheChangesThatItCopied()
        {
            var scheduler = new CompilationScheduler();
            scheduler.NoteInputsChanged();

            CompilationSnapshot snapshot = Take(scheduler);

            scheduler.HasUnabsorbedInputs().ShouldBeFalse();
            scheduler.HasMovedOnSince(snapshot).ShouldBeFalse();
        }

        [Fact]
        public void TakeSnapshot_Given_AChangeWhileItCopies_Then_TheChangeIsNotAbsorbed()
        {
            // The revision is read before the copies are taken. A change that lands in between is in the copies, and it is
            // counted as not yet seen: one more compilation, and never a lost edit.
            var scheduler = new CompilationScheduler();

            CompilationSnapshot snapshot = scheduler.TakeSnapshot(
                [],
                [],
                0,
                () =>
                {
                    scheduler.NoteInputsChanged();
                    return NoCopies();
                });

            scheduler.HasUnabsorbedInputs().ShouldBeTrue();
            scheduler.HasMovedOnSince(snapshot).ShouldBeTrue();
        }

        [Fact]
        public void HasMovedOnSince_Given_AChangeAfterTheSnapshot_Then_True()
        {
            var scheduler = new CompilationScheduler();
            CompilationSnapshot snapshot = Take(scheduler);
            scheduler.HasMovedOnSince(snapshot).ShouldBeFalse();

            scheduler.NoteInputsChanged();

            scheduler.HasMovedOnSince(snapshot).ShouldBeTrue();
        }

        #endregion

        #region Snapshots

        [Fact]
        public void TakeSnapshot_Then_CarriesWhatItWasGiven()
        {
            var scheduler = new CompilationScheduler();
            List<IResource<int, int>> resources = [];
            List<IWorkStream<int>> workStreams = [];
            List<IDependentActivity> copies = TwoActivities();
            Dictionary<int, IManagedActivityViewModel> sources = [];

            CompilationSnapshot snapshot = scheduler.TakeSnapshot(resources, workStreams, 7, () => (copies, sources));

            snapshot.Resources.ShouldBeSameAs(resources);
            snapshot.WorkStreams.ShouldBeSameAs(workStreams);
            snapshot.Activities.ShouldBeSameAs(copies);
            snapshot.Sources.ShouldBeSameAs(sources);
            snapshot.ScenarioRevision.ShouldBe(7);
        }

        [Fact]
        public void TakeSnapshot_Then_TheTicketsRiseInTheOrderOfTheSnapshots()
        {
            var scheduler = new CompilationScheduler();

            int first = Take(scheduler).Ticket;
            int second = Take(scheduler).Ticket;
            int third = Take(scheduler).Ticket;

            second.ShouldBeGreaterThan(first);
            third.ShouldBeGreaterThan(second);
        }

        [Fact]
        public void TryClaimPublication_Given_TheNewestSnapshot_Then_True()
        {
            var scheduler = new CompilationScheduler();

            scheduler.TryClaimPublication(Take(scheduler)).ShouldBeTrue();
        }

        [Fact]
        public void TryClaimPublication_Given_AnOlderSnapshotAfterANewerOneClaimed_Then_False()
        {
            var scheduler = new CompilationScheduler();
            CompilationSnapshot older = Take(scheduler);
            CompilationSnapshot newer = Take(scheduler);

            scheduler.TryClaimPublication(newer).ShouldBeTrue();

            scheduler.TryClaimPublication(older).ShouldBeFalse();
        }

        [Fact]
        public void TryClaimPublication_Given_TheOlderSnapshotFirst_Then_TheNewerOneMayStillClaim()
        {
            var scheduler = new CompilationScheduler();
            CompilationSnapshot older = Take(scheduler);
            CompilationSnapshot newer = Take(scheduler);

            scheduler.TryClaimPublication(older).ShouldBeTrue();

            scheduler.TryClaimPublication(newer).ShouldBeTrue();
        }

        [Fact]
        public void TryClaimPublication_Given_TheSameSnapshotTwice_Then_OnlyTheFirstClaimSucceeds()
        {
            var scheduler = new CompilationScheduler();
            CompilationSnapshot snapshot = Take(scheduler);

            scheduler.TryClaimPublication(snapshot).ShouldBeTrue();

            scheduler.TryClaimPublication(snapshot).ShouldBeFalse();
        }

        #endregion

        #region Drain

        [Fact]
        public void Drain_Given_AutoCompileOff_Then_CompilesNothing()
        {
            var scheduler = new CompilationScheduler();
            scheduler.NoteInputsChanged();
            int busy = 0;

            // A compilation that took no snapshot would leave the plan due, and a run that made it once would make it again
            // and again. The compilation throws, so that a fault in the check ends the test at the first.
            scheduler.Drain(
                () => false,
                () => busy++,
                () => busy++,
                () => throw new InvalidOperationException("a compilation was made while automatic compilation was off"));

            busy.ShouldBe(0, "a run that has nothing to do showed a busy window");
        }

        [Fact]
        public void Drain_Given_NothingDue_Then_CompilesNothing()
        {
            var scheduler = new CompilationScheduler();
            int busy = 0;

            scheduler.Drain(
                () => true,
                () => busy++,
                () => busy++,
                () => throw new InvalidOperationException("a compilation was made when the plan was not due one"));

            busy.ShouldBe(0, "a run that has nothing to do showed a busy window");
        }

        [Fact]
        public void Drain_Given_ARequest_Then_CompilesOnce()
        {
            var scheduler = new CompilationScheduler();
            scheduler.NoteInputsChanged();
            int compiles = 0;

            scheduler.Drain(
                () => true,
                () => { },
                () => { },
                () =>
                {
                    compiles++;
                    Take(scheduler);
                });

            compiles.ShouldBe(1);
            scheduler.HasUnabsorbedInputs().ShouldBeFalse();
        }

        [Fact]
        public void Drain_Given_SeveralEditsWhileACompilationRuns_Then_CompilesOnceMoreAndNoMore()
        {
            var scheduler = new CompilationScheduler();
            scheduler.NoteInputsChanged();
            int compiles = 0;

            scheduler.Drain(
                () => true,
                () => { },
                () => { },
                () =>
                {
                    compiles++;
                    Take(scheduler);

                    if (compiles == 1)
                    {
                        // Three edits while the first compilation runs: one compilation after it takes all of them.
                        scheduler.NoteInputsChanged();
                        scheduler.NoteInputsChanged();
                        scheduler.NoteInputsChanged();
                    }
                });

            compiles.ShouldBe(2);
            scheduler.HasUnabsorbedInputs().ShouldBeFalse();
        }

        [Fact]
        public void Drain_Given_SeveralCompilations_Then_OneBusyWindowCoversAllOfThem()
        {
            var scheduler = new CompilationScheduler();
            scheduler.NoteInputsChanged();
            List<string> events = [];
            int compiles = 0;

            scheduler.Drain(
                () => true,
                () => events.Add("begin"),
                () => events.Add("end"),
                () =>
                {
                    events.Add("compile");
                    Take(scheduler);

                    if (++compiles < 3)
                    {
                        scheduler.NoteInputsChanged();
                    }
                });

            events.ShouldBe(["begin", "compile", "compile", "compile", "end"]);
        }

        [Fact]
        public void Drain_Given_AutoCompileTurnedOffDuringARun_Then_StopsAtTheNextCheck()
        {
            var scheduler = new CompilationScheduler();
            scheduler.NoteInputsChanged();
            bool autoCompile = true;
            int compiles = 0;

            scheduler.Drain(
                () => autoCompile,
                () => { },
                () => { },
                () =>
                {
                    compiles++;
                    Take(scheduler);
                    autoCompile = false;
                    scheduler.NoteInputsChanged();

                    // Every compilation leaves the plan due again, so a run that does not stop would never end.
                    if (compiles > 3)
                    {
                        throw new InvalidOperationException("the run went on after automatic compilation was turned off");
                    }
                });

            compiles.ShouldBe(1);
            scheduler.HasUnabsorbedInputs().ShouldBeTrue();
        }

        [Fact]
        public void Drain_Given_ACompilationThatThrows_Then_EndsTheBusyWindowAndALaterRunIsNotShutOut()
        {
            var scheduler = new CompilationScheduler();
            scheduler.NoteInputsChanged();
            int begun = 0;
            int ended = 0;

            Should.Throw<InvalidOperationException>(() => scheduler.Drain(
                () => true,
                () => begun++,
                () => ended++,
                () => throw new InvalidOperationException()));

            begun.ShouldBe(1);
            ended.ShouldBe(1);

            // The failed compilation took no snapshot, so the plan is still due. The thread gave up the right to compile
            // on its way out, and another run is let in.
            int compiles = 0;

            scheduler.Drain(
                () => true,
                () => { },
                () => { },
                () =>
                {
                    compiles++;
                    Take(scheduler);
                });

            compiles.ShouldBe(1);
        }

        [Fact]
        public async Task Drain_Given_AnotherThreadMakingCompilations_Then_ReturnsAtOnceAndTheRunnerCompilesForTheRequest()
        {
            var scheduler = new CompilationScheduler();
            scheduler.NoteInputsChanged();
            using var inCompilation = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            int compiles = 0;

            void Compile()
            {
                int number = Interlocked.Increment(ref compiles);
                Take(scheduler);

                if (number == 1)
                {
                    inCompilation.Set();
                    release.Wait(s_Patience).ShouldBeTrue();
                }
            }

            Task runner = Task.Run(() => scheduler.Drain(() => true, () => { }, () => { }, Compile));
            inCompilation.Wait(s_Patience).ShouldBeTrue();

            // A request arrives while the runner is compiling. The thread that makes it leaves at once, and compiles nothing.
            scheduler.NoteInputsChanged();
            await Task.Run(() => scheduler.Drain(() => true, () => { }, () => { }, Compile)).WaitAsync(s_Patience);

            compiles.ShouldBe(1);

            release.Set();
            await runner.WaitAsync(s_Patience);

            // The runner looked at the revision again when it had finished, and it compiled for the request.
            compiles.ShouldBe(2);
            scheduler.HasUnabsorbedInputs().ShouldBeFalse();
        }

        [Fact]
        public void Drain_Given_ARequestInTheGapBeforeTheRunnerGivesUpTheRight_Then_TheRunnerCompilesForIt()
        {
            var scheduler = new CompilationScheduler();
            scheduler.NoteInputsChanged();
            int compiles = 0;
            int compilesInTheGap = -1;
            int ends = 0;

            void Compile()
            {
                compiles++;
                Take(scheduler);
            }

            scheduler.Drain(
                () => true,
                () => { },
                () =>
                {
                    // The end of the busy window falls after the last check of the inner loop and before the runner gives
                    // up the right to compile: this is the gap. A request made here finds the right held. The thread that
                    // makes it leaves at once, and only the check of the outer loop can see the request.
                    if (++ends == 1)
                    {
                        scheduler.NoteInputsChanged();
                        Task.Run(() => scheduler.Drain(() => true, () => { }, () => { }, Compile)).Wait(s_Patience).ShouldBeTrue();
                        compilesInTheGap = compiles;
                    }
                },
                Compile);

            compilesInTheGap.ShouldBe(1, "a thread that asked while the runner held the right made a compilation");
            compiles.ShouldBe(2, "the request that was made in the gap was lost");
            scheduler.HasUnabsorbedInputs().ShouldBeFalse();
        }

        #endregion

        #region Compile

        [Fact]
        public void Compile_Then_GivesThePlanCompilerTheCopiesTheResourcesAndTheWorkStreams()
        {
            var scheduler = new CompilationScheduler();
            List<IResource<int, int>> resources = [];
            List<IWorkStream<int>> workStreams = [];
            CompilationSnapshot snapshot = scheduler.TakeSnapshot(resources, workStreams, 0, () => (TwoActivities(), new Dictionary<int, IManagedActivityViewModel>()));
            bool called = false;

            IGraphCompilation<int, int, int, IDependentActivity> compilation = scheduler.Compile(
                snapshot,
                0,
                (compiler, availableResources, streams, token) =>
                {
                    called = true;
                    availableResources.ShouldBeSameAs(resources);
                    streams.ShouldBeSameAs(workStreams);
                    compiler.ActivityIds.ShouldBe([1, 2]);
                    return compiler.Compile(availableResources, streams, token);
                });

            called.ShouldBeTrue();
            compilation.CompilationErrors.ShouldBeEmpty();
            compilation.DependentActivities.Select(x => x.Id).Order().ShouldBe([1, 2]);
        }

        [Fact]
        public void Compile_Given_APlanCompilerThatOutlastsTheWatchdog_Then_ThrowsATimeoutException()
        {
            var scheduler = new CompilationScheduler();
            CompilationSnapshot snapshot = Take(scheduler);

            Should.Throw<GraphCompilationTimeoutException>(() => scheduler.Compile(
                snapshot,
                50,
                (compiler, availableResources, streams, token) =>
                {
                    token.WaitHandle.WaitOne(s_Patience);
                    token.ThrowIfCancellationRequested();
                    return compiler.Compile(availableResources, streams, token);
                }));
        }

        [Fact]
        public void Compile_Given_ACancellationThatIsNotTheWatchdog_Then_ItIsNotTurnedIntoATimeout()
        {
            var scheduler = new CompilationScheduler();
            CompilationSnapshot snapshot = Take(scheduler);

            // No budget, so there is no watchdog to blame.
            Exception thrown = Should.Throw<Exception>(() => scheduler.Compile(
                snapshot,
                0,
                (compiler, availableResources, streams, token) => throw new OperationCanceledException()));

            thrown.GetType().ShouldBe(typeof(OperationCanceledException));
        }

        #endregion

        #region Transitive reduction

        // Three activities: the second after the first, and the third after both. The third need not wait for the first.
        private static List<IDependentActivity> ThreeActivities()
        {
            var first = new DependentActivity(1, 3, canBeRemoved: false);
            var second = new DependentActivity(2, 4, canBeRemoved: false);
            var third = new DependentActivity(3, 2, canBeRemoved: false);
            second.Dependencies.Add(1);
            third.Dependencies.Add(1);
            third.Dependencies.Add(2);
            return [first, second, third];
        }

        // Compiles the copies as the core does.
        private static IGraphCompilation<int, int, int, IDependentActivity> Plan(
            VertexGraphCompiler compiler,
            List<IResource<int, int>> availableResources,
            List<IWorkStream<int>> workStreams,
            CancellationToken cancellationToken) =>
            compiler.Compile(availableResources, workStreams, cancellationToken);

        [Fact]
        public void ReduceTransitively_Given_ADependencyThatAnotherImplies_Then_TheReductionLacksItAndTheStateBeforeHoldsIt()
        {
            var scheduler = new CompilationScheduler();

            TransitiveReduction reduction = scheduler.ReduceTransitively(ThreeActivities(), 0, Plan);

            reduction.Compilation.CompilationErrors.ShouldBeEmpty();
            reduction.Before[3].Dependencies.ShouldBe(new HashSet<int> { 1, 2 }, ignoreOrder: true);
            reduction.After[3].Dependencies.ShouldBe(new HashSet<int> { 2 });
            reduction.After[2].Dependencies.ShouldBe(new HashSet<int> { 1 });
            reduction.After[1].Dependencies.ShouldBeEmpty();
        }

        [Fact]
        public void ReduceTransitively_Then_HasAnEntryForEveryCopyBeforeAndAfter()
        {
            var scheduler = new CompilationScheduler();

            TransitiveReduction reduction = scheduler.ReduceTransitively(ThreeActivities(), 0, Plan);

            reduction.Before.Keys.Order().ShouldBe([1, 2, 3]);
            reduction.After.Keys.Order().ShouldBe([1, 2, 3]);
        }

        [Fact]
        public void ReduceTransitively_Given_APlanningDependencyThatAnotherImplies_Then_TheReductionLacksItToo()
        {
            var scheduler = new CompilationScheduler();
            List<IDependentActivity> copies = ThreeActivities();
            copies[2].Dependencies.Remove(1);
            copies[2].PlanningDependencies.Add(1);

            TransitiveReduction reduction = scheduler.ReduceTransitively(copies, 0, Plan);

            reduction.Before[3].PlanningDependencies.ShouldBe(new HashSet<int> { 1 });
            reduction.After[3].PlanningDependencies.ShouldBeEmpty();
            reduction.After[3].Dependencies.ShouldBe(new HashSet<int> { 2 });
        }

        [Fact]
        public void ReduceTransitively_Given_ACopyThatHoldsADependencyOfAResource_Then_ItIsNotPartOfTheNetwork()
        {
            // The third activity waits for the second only because of a resource. It does not say so, and the dependency of
            // the user of the third on the first must stay.
            var scheduler = new CompilationScheduler();
            var first = new DependentActivity(1, 3, canBeRemoved: false);
            var second = new DependentActivity(2, 4, canBeRemoved: false);
            var third = new DependentActivity(3, 2, canBeRemoved: false);
            second.Dependencies.Add(1);
            third.Dependencies.Add(1);
            third.ResourceDependencies.Add(2);

            TransitiveReduction reduction = scheduler.ReduceTransitively([first, second, third], 0, Plan);

            reduction.Compilation.CompilationErrors.ShouldBeEmpty();
            reduction.After[3].Dependencies.ShouldBe(new HashSet<int> { 1 });
        }

        [Fact]
        public void ReduceTransitively_Given_ACompilationWithErrors_Then_ReturnsThemAndReducesNothing()
        {
            // Two activities that depend on each other, and a third with a dependency that is redundant.
            var scheduler = new CompilationScheduler();
            List<IDependentActivity> copies = ThreeActivities();
            copies[0].Dependencies.Add(2);

            TransitiveReduction reduction = scheduler.ReduceTransitively(copies, 0, Plan);

            reduction.Compilation.CompilationErrors.ShouldNotBeEmpty();
            reduction.After.Keys.Order().ShouldBe([1, 2, 3]);
            foreach (int id in reduction.After.Keys)
            {
                reduction.After[id].IsSameAs(reduction.Before[id]).ShouldBeTrue();
            }

            copies[2].Dependencies.ShouldBe(new HashSet<int> { 1, 2 }, ignoreOrder: true);
        }

        [Fact]
        public void ReduceTransitively_Then_GivesThePlanCompilerTheCopiesAndNoResourcesOrWorkStreams()
        {
            var scheduler = new CompilationScheduler();
            bool called = false;

            scheduler.ReduceTransitively(
                ThreeActivities(),
                0,
                (compiler, availableResources, streams, token) =>
                {
                    called = true;
                    availableResources.ShouldBeEmpty();
                    streams.ShouldBeEmpty();
                    compiler.ActivityIds.ShouldBe([1, 2, 3]);
                    return compiler.Compile(availableResources, streams, token);
                });

            called.ShouldBeTrue();
        }

        [Fact]
        public void ReduceTransitively_Given_APlanCompilerThatOutlastsTheWatchdog_Then_ThrowsATimeoutException()
        {
            var scheduler = new CompilationScheduler();

            Should.Throw<GraphCompilationTimeoutException>(() => scheduler.ReduceTransitively(
                ThreeActivities(),
                50,
                (compiler, availableResources, streams, token) =>
                {
                    token.WaitHandle.WaitOne(s_Patience);
                    token.ThrowIfCancellationRequested();
                    return compiler.Compile(availableResources, streams, token);
                }));
        }

        [Fact]
        public void ReduceTransitively_Given_ACancellationThatIsNotTheWatchdog_Then_ItIsNotTurnedIntoATimeout()
        {
            var scheduler = new CompilationScheduler();

            // No budget, so there is no watchdog to blame.
            Exception thrown = Should.Throw<Exception>(() => scheduler.ReduceTransitively(
                ThreeActivities(),
                0,
                (compiler, availableResources, streams, token) => throw new OperationCanceledException()));

            thrown.GetType().ShouldBe(typeof(OperationCanceledException));
        }

        #endregion
    }
}
