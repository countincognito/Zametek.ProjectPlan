using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IResourceMetricManagerViewModel
        : IKillSubscriptions, IDisposable
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        bool HideCost { get; }

        bool HideBilling { get; }

        List<ResourceMetricsModel> ResourceMetrics { get; }
    }
}
