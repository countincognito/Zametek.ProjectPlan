using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// What a cascade builds from one <see cref="CascadeInputs"/>. The core publishes all of it together, or none of it.
    /// </summary>
    internal sealed record CascadeOutputs(
        ArrowGraphModel ArrowGraph,
        VertexGraphModel VertexGraph,
        ResourceSeriesSetModel ResourceSeriesSet,
        TrackingSeriesSetModel TrackingSeriesSet,
        NetworkModel NetworkMetrics,
        RisksModel RiskMetrics,
        CostsModel CostMetrics,
        BillingsModel BillingMetrics,
        MarginsModel MarginMetrics,
        EffortsModel EffortMetrics,
        List<ResourceMetricsModel> ResourceMetrics);
}
