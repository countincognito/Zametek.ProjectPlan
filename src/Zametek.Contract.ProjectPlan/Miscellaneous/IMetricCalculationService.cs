using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface IMetricCalculationService
    {
        NetworkModel BuildNetworkMetrics(
            IGraphCompilation<int, int, int, IDependentActivity> graphCompilation,
            bool hasCompilationErrors,
            DateTimeOffset projectStart,
            int? startTime,
            int? finishTime);

        RisksModel BuildRiskMetrics(
            IGraphCompilation<int, int, int, IDependentActivity> graphCompilation,
            bool hasCompilationErrors,
            IEnumerable<ActivitySeverityModel> activitySeverities);

        (CostsModel costs, BillingsModel billings, MarginsModel margins, EffortsModel efforts)
            BuildFinancialMetrics(
            ResourceSeriesSetModel resourceSeriesSet,
            bool hasCompilationErrors);
    }
}
