using System;
using System.Collections.Generic;
using System.Threading;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A graph service whose next build of the arrow graph a test can hold. The arrow graph is the first output of
    /// the cascade that follows a compilation, so holding it holds the cascade part way through: after it has copied
    /// what it reads, and so without the core lock, and before it publishes.
    /// </summary>
    public sealed class GatedGraphCompilationService
        : IGraphCompilationService
    {
        private static readonly TimeSpan s_Patience = TimeSpan.FromSeconds(30);

        private readonly IGraphCompilationService m_Inner;
        private readonly ManualResetEventSlim m_Open = new(true);
        private readonly SemaphoreSlim m_Held = new(0);
        private int m_HoldNext;
        private int m_Started;

        public GatedGraphCompilationService(IGraphCompilationService inner)
        {
            ArgumentNullException.ThrowIfNull(inner);
            m_Inner = inner;
        }

        /// <summary>
        /// How many cascades have reached the build of the arrow graph, the held one included.
        /// </summary>
        public int CascadesStarted => Volatile.Read(ref m_Started);

        public void HoldNextCascade()
        {
            m_Open.Reset();
            Volatile.Write(ref m_HoldNext, 1);
        }

        public void WaitUntilHeld()
        {
            if (!m_Held.Wait(s_Patience))
            {
                throw new TimeoutException(@"No cascade reached the gate.");
            }
        }

        public void ReleaseHeldCascade() => m_Open.Set();

        public ArrowGraphModel BuildArrowGraph(IEnumerable<IDependentActivity> dependentActivities)
        {
            Interlocked.Increment(ref m_Started);

            if (Interlocked.Exchange(ref m_HoldNext, 0) == 1)
            {
                m_Held.Release();

                if (!m_Open.Wait(s_Patience))
                {
                    throw new TimeoutException(@"The held cascade was never released.");
                }
            }

            return m_Inner.BuildArrowGraph(dependentActivities);
        }

        public VertexGraphModel BuildVertexGraph(
            IEnumerable<IDependentActivity> dependentActivities,
            IEnumerable<ResourceModel> resources,
            bool resourcesAreDisabled,
            IEnumerable<WorkStreamModel> workStreams) =>
            m_Inner.BuildVertexGraph(dependentActivities, resources, resourcesAreDisabled, workStreams);
    }
}
