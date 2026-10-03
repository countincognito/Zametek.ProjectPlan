using ReactiveUI;
using Xunit;
using ReactiveUI.Primitives.Concurrency;
using System.Diagnostics;

namespace Zametek.Engine.ProjectPlan.Tests
{
    /// <summary>
    /// A sequencer that queues work instead of running it, so that a test can see what a view model hands to another
    /// thread, and can hold it back.
    /// </summary>
    internal sealed class ManualSequencer
        : ISequencer
    {
        private readonly Lock m_Lock = new();
        private readonly Queue<IWorkItem> m_Queue = new();

        // Real time, because nothing here schedules against a clock.
        public DateTimeOffset Now => DateTimeOffset.Now;

        public long Timestamp => Stopwatch.GetTimestamp();

        /// <summary>
        /// How much work has been handed over and not run.
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

        public void Schedule(IWorkItem item, long dueTimestamp) => Schedule(item);

        /// <summary>
        /// Runs everything queued, and everything that queues while running.
        /// </summary>
        public void Drain()
        {
            while (true)
            {
                IWorkItem item;

                lock (m_Lock)
                {
                    if (m_Queue.Count == 0)
                    {
                        return;
                    }

                    item = m_Queue.Dequeue();
                }

                item.Execute();
            }
        }
    }

    /// <summary>
    /// Holds both of ReactiveUI's schedulers, the thread pool's and the main thread's, as <see cref="ManualSequencer"/>s
    /// for as long as it lives, and puts the real ones back afterwards. The schedulers are process-wide, so a test that
    /// uses this belongs to <see cref="SchedulerSwapCollection"/>, which runs alone.
    /// </summary>
    internal sealed class ManualSchedulersScope
        : IDisposable
    {
        private readonly ISequencer m_PreviousMain;
        private readonly ISequencer m_PreviousPool;

        public ManualSchedulersScope()
        {
            m_PreviousMain = RxSchedulers.MainThreadScheduler;
            m_PreviousPool = RxSchedulers.TaskpoolScheduler;
            Main = new ManualSequencer();
            Pool = new ManualSequencer();
            RxSchedulers.MainThreadScheduler = Main;
            RxSchedulers.TaskpoolScheduler = Pool;
        }

        public ManualSequencer Main { get; }

        public ManualSequencer Pool { get; }

        /// <summary>
        /// Runs what both pumps hold, and what each hands to the other as it runs, until neither has any left.
        /// </summary>
        public void Drain()
        {
            while (Pool.PendingCount > 0 || Main.PendingCount > 0)
            {
                Pool.Drain();
                Main.Drain();
            }
        }

        public void Dispose()
        {
            RxSchedulers.MainThreadScheduler = m_PreviousMain;
            RxSchedulers.TaskpoolScheduler = m_PreviousPool;
        }
    }

    /// <summary>
    /// The tests that swap ReactiveUI's schedulers. They run on their own, because a job running at the same time in
    /// another test would have its deliveries queued into a pump nobody drains.
    /// </summary>
    [CollectionDefinition(nameof(SchedulerSwapCollection), DisableParallelization = true)]
    public class SchedulerSwapCollection;
}
