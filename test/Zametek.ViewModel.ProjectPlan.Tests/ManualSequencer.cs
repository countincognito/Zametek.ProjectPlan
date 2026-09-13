using ReactiveUI;
using ReactiveUI.Primitives.Concurrency;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A sequencer that queues work instead of running it, so a test decides when
    /// deferred deliveries happen.
    /// </summary>
    /// <remarks>
    /// This is the piece that makes ordering testable at all. The application's
    /// deferred subscriptions all hand their work to
    /// <c>RxSchedulers.MainThreadScheduler</c>, and in the running application that is
    /// the Avalonia dispatcher: a first-in-first-out queue drained on the user
    /// interface thread at a moment nothing in the view models controls. Substituting
    /// this pump reproduces the same first-in-first-out behaviour while handing the
    /// drain point to the test, which is what turns a race into an assertion.
    /// Draining loops until the queue is empty rather than taking a single pass,
    /// because executing a work item can schedule more (an <c>ObserveOn</c> reschedules
    /// its own drain while it has values pending).
    /// </remarks>
    internal sealed class ManualSequencer
        : ISequencer
    {
        private readonly Lock m_Lock = new();
        private readonly Queue<IWorkItem> m_Queue = new();

        // Real time, because nothing here schedules against a clock; the sequencer's own
        // static helpers for these are not public.
        public DateTimeOffset Now => DateTimeOffset.Now;

        public long Timestamp => Stopwatch.GetTimestamp();

        /// <summary>
        /// How much work is waiting. A test asserting that a delivery was deferred
        /// rather than run inline reads this before draining.
        /// </summary>
        public int PendingCount
        {
            get
            {
                lock (m_Lock)
                {
                    return m_Queue.Count;
                }
            }
        }

        public void Schedule(IWorkItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            lock (m_Lock)
            {
                m_Queue.Enqueue(item);
            }
        }

        // Due times are irrelevant to a pump with no clock: everything queued is run by
        // the next Drain, in the order it was scheduled. None of the subscriptions under
        // test schedule delayed work. Cancellation needs nothing here either - a caller
        // cancels by disposing the work item, which Drain checks for.
        public void Schedule(IWorkItem item, long dueTimestamp) => Schedule(item);

        /// <summary>
        /// Runs everything queued, and everything that queues while running, returning
        /// the number of work items executed.
        /// </summary>
        public int Drain()
        {
            int executed = 0;

            while (true)
            {
                IWorkItem item;

                lock (m_Lock)
                {
                    if (m_Queue.Count == 0)
                    {
                        return executed;
                    }

                    item = m_Queue.Dequeue();
                }

                // Executed unconditionally. The real sequencers skip work cancelled
                // between scheduling and running, but the helper that tests for that is
                // not public, and a test drains before it tears anything down, so there
                // is never a cancelled item in the queue here.
                item.Execute();
                executed++;
            }
        }
    }

    /// <summary>
    /// Installs a <see cref="ManualSequencer"/> as the main thread scheduler for the
    /// duration of a test, and puts the previous one back afterwards.
    /// </summary>
    internal sealed class MainThreadSequencerScope
        : IDisposable
    {
        private readonly ISequencer m_Previous;

        public MainThreadSequencerScope()
        {
            m_Previous = RxSchedulers.MainThreadScheduler;
            Pump = new ManualSequencer();
            RxSchedulers.MainThreadScheduler = Pump;
        }

        public ManualSequencer Pump { get; }

        public void Dispose()
        {
            RxSchedulers.MainThreadScheduler = m_Previous;
        }
    }
}
