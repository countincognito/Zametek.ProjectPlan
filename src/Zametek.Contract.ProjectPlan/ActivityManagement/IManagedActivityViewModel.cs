using System;
using System.Collections.Generic;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface IManagedActivityViewModel
        : IDependentActivity<int, int>
    {
        DateTime ProjectStart { get; set; }

        string DependenciesString { get; set; }

        HashSet<int> UpdatedDependencies { get; }

        bool HasUpdatedDependencies { get; set; }

        string ResourceDependenciesString { get; }

        public string AllocatedToResourcesString { get; }

        DateTime? EarliestStartDateTime { get; }

        DateTime? LatestStartDateTime { get; }

        DateTime? EarliestFinishDateTime { get; }

        DateTime? LatestFinishDateTime { get; }

        DateTime? MinimumEarliestStartDateTime { get; set; }

        DateTime? MaximumLatestFinishDateTime { get; set; }

        void UseBusinessDays(bool useBusinessDays);

        void SetTargetResources(IEnumerable<ResourceModel> targetResources);

        void UpdateAllocatedToResources();
    }
}
