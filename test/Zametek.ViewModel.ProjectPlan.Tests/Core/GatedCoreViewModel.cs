using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A core whose next compilation a test can hold part way through: after the copy of the plan is taken, and so
    /// without the core lock, and before the results are published.
    /// </summary>
    /// <remarks>
    /// This is what makes the edits that arrive during a compilation testable. A compilation dominates the time it
    /// takes, so an edit made at a random moment hardly ever lands in the window between the copy and the
    /// publication. Holding the compilation puts the test in that window every time.
    /// <para>
    /// Only the compilation that is started after <see cref="HoldNextCompilation"/> is held. The ones before and after
    /// it run straight through, and so a test holds one and lets the follow-up pass.
    /// </para>
    /// </remarks>
    public sealed class GatedCoreViewModel
        : CoreViewModel
    {
        private static readonly TimeSpan s_Patience = TimeSpan.FromSeconds(30);

        private readonly ManualResetEventSlim m_Open = new(true);
        private readonly SemaphoreSlim m_Held = new(0);
        private int m_HoldNext;
        private int m_StallNext;
        private int m_Started;

        public GatedCoreViewModel(
            IProjectScenarioFileImport projectScenarioFileImport,
            IProjectScenarioFileExport projectScenarioFileExport,
            ISettingService settingService,
            IDateTimeCalculator dateTimeCalculator,
            ProjectPlanMapper mapper,
            IGraphCompilationService graphCompilationService,
            IResourceSchedulingService resourceSchedulingService,
            IMetricCalculationService metricCalculationService,
            IDataGridScrollManager dataGridScrollManager,
            ILogger<CoreViewModel> logger)
            : base(
                projectScenarioFileImport,
                projectScenarioFileExport,
                settingService,
                dateTimeCalculator,
                mapper,
                graphCompilationService,
                resourceSchedulingService,
                metricCalculationService,
                dataGridScrollManager,
                logger)
        {
        }

        /// <summary>
        /// How many compilations have reached the compile step, the held one included.
        /// </summary>
        public int CompilationsStarted => Volatile.Read(ref m_Started);

        public void HoldNextCompilation()
        {
            m_Open.Reset();
            Volatile.Write(ref m_HoldNext, 1);
        }

        /// <summary>
        /// Blocks until the held compilation has taken its copy and is waiting to be released.
        /// </summary>
        public void WaitUntilHeld()
        {
            if (!m_Held.Wait(s_Patience))
            {
                throw new TimeoutException(@"No compilation reached the gate.");
            }
        }

        public void ReleaseHeldCompilation() => m_Open.Set();

        /// <summary>
        /// Makes the next compilation outlast the watchdog: it waits for the watchdog to cancel it, and then it ends as a
        /// cancelled compilation does. The core needs a budget for this, and so the test makes it with one.
        /// </summary>
        public void StallNextCompilation() => Volatile.Write(ref m_StallNext, 1);

        protected override IGraphCompilation<int, int, int, IDependentActivity> CompilePlan(
            VertexGraphCompiler compiler,
            List<IResource<int, int>> availableResources,
            List<IWorkStream<int>> workStreams,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref m_Started);

            if (Interlocked.Exchange(ref m_HoldNext, 0) == 1)
            {
                m_Held.Release();

                if (!m_Open.Wait(s_Patience))
                {
                    throw new TimeoutException(@"The held compilation was never released.");
                }
            }

            if (Interlocked.Exchange(ref m_StallNext, 0) == 1)
            {
                cancellationToken.WaitHandle.WaitOne(s_Patience);
                cancellationToken.ThrowIfCancellationRequested();
            }

            return base.CompilePlan(compiler, availableResources, workStreams, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // A test that failed while it held a compilation must not leave the thread waiting.
                m_Open.Set();
            }

            base.Dispose(disposing);
        }
    }
}
