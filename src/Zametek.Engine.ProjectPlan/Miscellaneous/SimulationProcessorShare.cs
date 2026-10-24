namespace Zametek.Engine.ProjectPlan
{
    // How the processors are shared by the simulations that run at one time in one process. A simulation asks the thread pool for
    // as many threads as the machine has processors, and a server that runs several of them at once would ask for several times
    // the processors it has. The result of a simulation does not depend on the number of threads, and so a simulation can be
    // given less without a change to what it finds.
    //
    // A simulation that begins takes the processors divided by the number of simulations that run then, itself among them, and
    // at least one: the first gets all of them, the second half, the third a third, and so on. A simulation keeps its share to
    // its end. When several begin together, they share the processors equally. When one begins later, it takes less than the
    // ones before it, and thus the threads that all of them ask for add up to less than the processors times the number of
    // simulations. A single job (zpp) is alone, and has all the processors.
    public sealed class SimulationProcessorShare
    {
        private readonly int m_Processors;
        private int m_Running;

        public SimulationProcessorShare()
            : this(Environment.ProcessorCount)
        {
        }

        public SimulationProcessorShare(int processors)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(processors, 1);
            m_Processors = processors;
        }

        // The processors that the machine has, for the purpose of the share.
        public int Processors => m_Processors;

        // The number of simulations that run now.
        public int Running => Volatile.Read(ref m_Running);

        // A simulation begins. The lease says how many threads it may use, and it ends when the lease is disposed of.
        public Lease Begin()
        {
            int running = Interlocked.Increment(ref m_Running);
            return new Lease(this, Math.Max(1, m_Processors / running));
        }

        private void End()
        {
            Interlocked.Decrement(ref m_Running);
        }

        // The share of one simulation.
        public sealed class Lease
            : IDisposable
        {
            private readonly SimulationProcessorShare m_Share;
            private int m_Ended;

            internal Lease(SimulationProcessorShare share, int threads)
            {
                m_Share = share;
                Threads = threads;
            }

            // The most threads that the simulation uses.
            public int Threads { get; }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref m_Ended, 1) == 0)
                {
                    m_Share.End();
                }
            }
        }
    }
}
