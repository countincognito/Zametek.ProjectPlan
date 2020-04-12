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

        bool UseBusinessDays { set; }

        string DependenciesString { get; set; }

        HashSet<int> UpdatedDependencies { get; }

        bool HasUpdatedDependencies { get; set; }

        string ResourceDependenciesString { get; }

        void SetTargetResources(IEnumerable<ResourceModel> targetResources);

        void UpdateAllocatedToResources();

        public string AllocatedToResourcesString { get; }
    }
}
