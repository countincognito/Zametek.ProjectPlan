using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Decides when the plan is compiled, and compiles a copy of it. It counts the changes of what a compilation reads, so
    /// that a compilation can tell when it has finished whether the plan moved on. It numbers the snapshots, so that an
    /// older compilation never publishes over a newer one. It lets one thread at a time make the compilations that the
    /// edits ask for (ARCHITECTURE section 7 rule 14).
    /// </summary>
    /// <remarks>
    /// It has no lock. The members that say so are called with the lock of the core held, and the others can be called
    /// from any thread.
    /// </remarks>
    internal sealed class CompilationScheduler
    {
        #region Types

        /// <summary>
        /// Compiles the copy of the plan. The core gives it, so that a test can hold a compilation part way through.
        /// </summary>
        public delegate IGraphCompilation<int, int, int, IDependentActivity> CompilePlanDelegate(
            VertexGraphCompiler compiler,
            List<IResource<int, int>> availableResources,
            List<IWorkStream<int>> workStreams,
            CancellationToken cancellationToken);

        #endregion

        #region Fields

        // What a compilation reads has a revision: a count of everything that changes it. A compilation records the
        // revision that it started from, and so it can tell when it has finished whether the plan has moved on
        // (ARCHITECTURE section 7 rule 14). The count only has to differ from one moment to the next, and so it wraps.
        private int m_InputRevision;

        // The revision that the newest snapshot started from. The plan is due a compilation when the two differ. Written
        // with the lock of the core held, and read without it.
        private int m_SnapshotRevision;

        // Number the snapshots in the order they are taken, so that the results of an older snapshot never replace the
        // results of a newer one that has published already. Used with the lock of the core held only.
        private int m_SnapshotTicket;
        private int m_PublishedTicket;

        // 1 while one thread makes the compilations that the edits ask for (see Drain), and 0 otherwise.
        private int m_AutoCompileRunning;

        #endregion

        #region Methods

        /// <summary>
        /// Counts a change of something that a compilation reads. A compilation that is running did not see the
        /// change, so it publishes its results as stale, and the plan is compiled again.
        /// </summary>
        public void NoteInputsChanged() => Interlocked.Increment(ref m_InputRevision);

        /// <summary>
        /// Whether the snapshot is out of date: something that a compilation reads has changed since it was taken.
        /// </summary>
        public bool HasMovedOnSince(CompilationSnapshot snapshot) =>
            Volatile.Read(ref m_InputRevision) != snapshot.InputRevision;

        /// <summary>
        /// Whether the results of the compilation of the snapshot may be published. They may not if the results of a later
        /// snapshot have been published already. When they may, the claim is recorded, and no older snapshot may publish
        /// after this one. Call it with the lock of the core held, as the last of the checks of the publication.
        /// </summary>
        public bool TryClaimPublication(CompilationSnapshot snapshot)
        {
            if (snapshot.Ticket > m_PublishedTicket)
            {
                m_PublishedTicket = snapshot.Ticket;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Takes the snapshot of the plan that a compilation works on. Call it with the lock of the core held: the copy
        /// reads the live graph, which the structural edits change under that lock.
        /// </summary>
        /// <param name="availableResources">The resources that the compilation schedules.</param>
        /// <param name="workStreams">The work streams that the compilation uses.</param>
        /// <param name="scenarioRevision">The scenario that the copies belong to.</param>
        /// <param name="copyActivities">Copies every activity, in the order of the live graph, with the live activity that each copy came from.</param>
        public CompilationSnapshot TakeSnapshot(
            List<IResource<int, int>> availableResources,
            List<IWorkStream<int>> workStreams,
            int scenarioRevision,
            Func<(List<IDependentActivity> Copies, Dictionary<int, IManagedActivityViewModel> Sources)> copyActivities)
        {
            // The revision is read before the copies are taken. An edit that lands in between is then
            // in the copies and counted as not yet seen, which costs one more compilation and never
            // loses the edit.
            int inputRevision = Volatile.Read(ref m_InputRevision);

            (List<IDependentActivity> activityCopies, Dictionary<int, IManagedActivityViewModel> sources) = copyActivities();

            Volatile.Write(ref m_SnapshotRevision, inputRevision);

            return new CompilationSnapshot(
                activityCopies,
                sources,
                availableResources,
                workStreams,
                inputRevision,
                scenarioRevision,
                ++m_SnapshotTicket);
        }

        /// <summary>
        /// Builds a throwaway compiler that holds the copies. It needs no lock: nothing else can reach it.
        /// </summary>
        private static VertexGraphCompiler BuildCompiler(IEnumerable<IDependentActivity> activityCopies)
        {
            var compiler = new VertexGraphCompiler();

            foreach (IDependentActivity activityCopy in activityCopies)
            {
                compiler.AddActivity(activityCopy);
            }

            return compiler;
        }

        /// <summary>
        /// Compiles a snapshot under the watchdog, turning a watchdog cancellation into a
        /// <see cref="GraphCompilationTimeoutException"/> so that callers can tell it apart
        /// from any other cancellation.
        /// </summary>
        /// <remarks>
        /// The compiler works on copies rather than on the live activities, so an edit made
        /// while it runs cannot corrupt what it is compiling, and it cannot be seen writing
        /// its results one activity at a time. A compilation that is abandoned - because it
        /// timed out, or because it failed - therefore publishes nothing at all, and the
        /// previous results stay in place whole.
        /// <para>
        /// It needs no lock, and its caller holds none unless a bulk update is running it. The
        /// budget starts immediately around the compile, so neither the wait for the lock of the core
        /// nor the copying is charged to it.
        /// </para>
        /// </remarks>
        public IGraphCompilation<int, int, int, IDependentActivity> Compile(
            CompilationSnapshot snapshot,
            int timeoutMilliseconds,
            CompilePlanDelegate compilePlan)
        {
            VertexGraphCompiler compiler = BuildCompiler(snapshot.Activities);

            using CancellationTokenSource? timeoutSource = CompilationTimeoutHelper.CreateTimeoutSource(timeoutMilliseconds);

            try
            {
                return compilePlan(
                    compiler,
                    snapshot.Resources,
                    snapshot.WorkStreams,
                    CompilationTimeoutHelper.TokenOrNone(timeoutSource));
            }
            catch (OperationCanceledException ex) when (timeoutSource is not null && timeoutSource.IsCancellationRequested)
            {
                throw CompilationTimeoutHelper.TimedOut(timeoutMilliseconds, ex);
            }
        }

        /// <summary>
        /// Takes away the dependencies of the user that other dependencies of the user imply, from the copies. It compiles
        /// the copies with infinite resources under the watchdog, and reduces them in the compiler that holds them.
        /// </summary>
        /// <remarks>
        /// The dependencies that a resource gives are dynamic, and the reduction must not see them: it would take away a
        /// dependency of the user that only a resource implies. The compilation takes them out of the network of the
        /// compiler, and so the reduction that follows works on the dependencies of the user alone. The compilation also
        /// checks the copies. When it finds errors the copies are not reduced, and the caller says why.
        /// <para>
        /// It changes the copies and nothing else, and what it gives back says what each of them held before and after.
        /// It needs no lock, and its caller holds one for as long as the result must stay true of the plan. The budget
        /// of the watchdog starts immediately around the compile.
        /// </para>
        /// </remarks>
        /// <param name="activityCopies">The copies of the activities, in the order of the live graph.</param>
        /// <param name="timeoutMilliseconds">The budget of the compilation, or 0 for none.</param>
        /// <param name="compilePlan">Compiles the copies. The core gives it, so that a test can hold a compilation part way through.</param>
        public TransitiveReduction ReduceTransitively(
            IReadOnlyList<IDependentActivity> activityCopies,
            int timeoutMilliseconds,
            CompilePlanDelegate compilePlan)
        {
            Dictionary<int, ActivityDependencies> before = activityCopies.ToDictionary(x => x.Id, ActivityDependencies.Of);

            VertexGraphCompiler compiler = BuildCompiler(activityCopies);

            IGraphCompilation<int, int, int, IDependentActivity> graphCompilation;

            using (CancellationTokenSource? timeoutSource = CompilationTimeoutHelper.CreateTimeoutSource(timeoutMilliseconds))
            {
                try
                {
                    // No resources: the compilation is the pure critical path schedule.
                    graphCompilation = compilePlan(compiler, [], [], CompilationTimeoutHelper.TokenOrNone(timeoutSource));
                }
                catch (OperationCanceledException ex) when (timeoutSource is not null && timeoutSource.IsCancellationRequested)
                {
                    throw CompilationTimeoutHelper.TimedOut(timeoutMilliseconds, ex);
                }
            }

            if (graphCompilation.CompilationErrors.Any())
            {
                return new TransitiveReduction(graphCompilation, before, before);
            }

            compiler.TransitiveReduction();

            return new TransitiveReduction(
                graphCompilation,
                before,
                activityCopies.ToDictionary(x => x.Id, ActivityDependencies.Of));
        }

        /// <summary>
        /// Makes the compilations that the edits of the plan ask for, one at a time, until the plan is not due another.
        /// </summary>
        /// <remarks>
        /// At most one thread makes them. A thread that finds another making them leaves, and its request waits in the
        /// revision: the thread that is making them compares the revision with that of its snapshot when it has
        /// published, and compiles again if they differ. So the edits made while a compilation runs are compiled
        /// together, once, after it - neither cancelled into the compilation that is running, nor lost, nor compiled
        /// one by one. The check of the outer loop closes the gap in which a request arrives after the last check of
        /// the inner loop and before the thread has given up the right to compile.
        /// </remarks>
        /// <param name="isAutoCompileOn">Says whether automatic compilation is on, each time it is asked.</param>
        /// <param name="beginBusy">Starts the busy signal of the core. It is called once for all the compilations of a run.</param>
        /// <param name="endBusy">Ends the busy signal of the core.</param>
        /// <param name="compile">Makes one compilation. The compilation takes a snapshot, and with it the plan is not due again.</param>
        public void Drain(Func<bool> isAutoCompileOn, Action beginBusy, Action endBusy, Action compile)
        {
            while (IsDue(isAutoCompileOn()))
            {
                if (Interlocked.CompareExchange(ref m_AutoCompileRunning, 1, 0) != 0)
                {
                    return;
                }

                try
                {
                    // One busy window for all of them, and not one for each, so that the signal does not flap.
                    beginBusy();

                    try
                    {
                        while (IsDue(isAutoCompileOn()))
                        {
                            compile();
                        }
                    }
                    finally
                    {
                        endBusy();
                    }
                }
                finally
                {
                    Volatile.Write(ref m_AutoCompileRunning, 0);
                }
            }
        }

        // Automatic compilation is on, and something that a compilation reads has changed since the newest snapshot.
        private bool IsDue(bool autoCompile) => autoCompile && HasUnabsorbedInputs();

        public bool HasUnabsorbedInputs() =>
            Volatile.Read(ref m_InputRevision) != Volatile.Read(ref m_SnapshotRevision);

        // Takes everything that has changed so far as already compiled: a reset has nothing to compile, and switching
        // automatic compilation on compiles nothing until the next edit.
        public void AbsorbPendingInputs() =>
            Volatile.Write(ref m_SnapshotRevision, Volatile.Read(ref m_InputRevision));

        #endregion
    }
}
