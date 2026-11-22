using System;
using System.Collections.Generic;
using System.Threading;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A metric service that a test can make fail: the network metrics, which the build of the outputs after a compilation
    /// calculates, and the risk metrics, which a change of the graph settings calculates too. It does the work of the
    /// service that it is given when it is told of no failure.
    /// </summary>
    public sealed class FaultyMetricCalculationService
        : IMetricCalculationService
    {
        private readonly IMetricCalculationService m_Inner;
        private int m_RiskBuilds;

        public FaultyMetricCalculationService(IMetricCalculationService inner)
        {
            ArgumentNullException.ThrowIfNull(inner);
            m_Inner = inner;
        }

        /// <summary>What the network metrics throw, or null when they work.</summary>
        public Exception? NetworkFailure { get; set; }

        /// <summary>What the risk metrics throw, or null when they work.</summary>
        public Exception? RiskFailure { get; set; }

        /// <summary>How many times the risk metrics were built without a failure.</summary>
        public int RiskBuilds => Volatile.Read(ref m_RiskBuilds);

        public NetworkModel BuildNetworkMetrics(
            IGraphCompilation<int, int, int, IDependentActivity> graphCompilation,
            bool hasCompilationErrors,
            DateTimeOffset projectStart,
            int? startTime,
            int? finishTime)
        {
            if (NetworkFailure is not null)
            {
                throw NetworkFailure;
            }

            return m_Inner.BuildNetworkMetrics(graphCompilation, hasCompilationErrors, projectStart, startTime, finishTime);
        }

        public RisksModel BuildRiskMetrics(
            IGraphCompilation<int, int, int, IDependentActivity> graphCompilation,
            bool hasCompilationErrors,
            IEnumerable<ActivitySeverityModel> activitySeverities)
        {
            if (RiskFailure is not null)
            {
                throw RiskFailure;
            }

            RisksModel risks = m_Inner.BuildRiskMetrics(graphCompilation, hasCompilationErrors, activitySeverities);
            Interlocked.Increment(ref m_RiskBuilds);
            return risks;
        }

        public (CostsModel costs, BillingsModel billings, MarginsModel margins, EffortsModel efforts, List<ResourceMetricsModel> resourceMetrics)
            BuildFinancialMetrics(
            ResourceSeriesSetModel resourceSeriesSet,
            bool hasCompilationErrors) =>
            m_Inner.BuildFinancialMetrics(resourceSeriesSet, hasCompilationErrors);

        public HeadcountModel BuildHeadcountMetrics(
            ResourceSeriesSetModel resourceSeriesSet,
            bool hasCompilationErrors) =>
            m_Inner.BuildHeadcountMetrics(resourceSeriesSet, hasCompilationErrors);
    }
}
