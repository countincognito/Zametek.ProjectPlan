using System;
using System.Collections.Generic;
using System.Text;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class BasicManagedActivityViewModel
        : IManagedActivityViewModel
    {
        public BasicManagedActivityViewModel(IDependentActivity<int, int> dependentActivity)
        {
            DependentActivity = dependentActivity ?? throw new ArgumentNullException(nameof(dependentActivity));
        }

        public IDependentActivity<int, int> DependentActivity { get; }

        public HashSet<int> Dependencies => DependentActivity.Dependencies;

        public HashSet<int> ResourceDependencies => DependentActivity.ResourceDependencies;

        public string Name { get => DependentActivity.Name; set => DependentActivity.Name = value; }

        public HashSet<int> TargetResources => DependentActivity.TargetResources;

        public LogicalOperator TargetResourceOperator { get => DependentActivity.TargetResourceOperator; set => DependentActivity.TargetResourceOperator = value; }

        public HashSet<int> AllocatedToResources => DependentActivity.AllocatedToResources;

        public bool IsDummy => DependentActivity.IsDummy;

        public int Duration { get => DependentActivity.Duration; set => DependentActivity.Duration = value; }

        public int? TotalSlack => DependentActivity.TotalSlack;

        public int? FreeSlack { get => DependentActivity.FreeSlack; set => DependentActivity.FreeSlack = value; }

        public int? InterferingSlack => DependentActivity.InterferingSlack;

        public bool IsCritical => DependentActivity.IsCritical;

        public int? EarliestStartTime { get => DependentActivity.EarliestStartTime; set => DependentActivity.EarliestStartTime = value; }

        public int? LatestStartTime => DependentActivity.LatestStartTime;

        public int? EarliestFinishTime => DependentActivity.EarliestFinishTime;

        public int? LatestFinishTime { get => DependentActivity.LatestFinishTime; set => DependentActivity.LatestFinishTime = value; }
        public int? MinimumFreeSlack { get => DependentActivity.MinimumFreeSlack; set => DependentActivity.MinimumFreeSlack = value; }
        public int? MinimumEarliestStartTime { get => DependentActivity.MinimumEarliestStartTime; set => DependentActivity.MinimumEarliestStartTime = value; }

        public int Id => DependentActivity.Id;

        public bool CanBeRemoved => DependentActivity.CanBeRemoved;






        public DateTime ProjectStart { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool UseBusinessDays { set => throw new NotImplementedException(); }
        public string DependenciesString { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public HashSet<int> UpdatedDependencies => throw new NotImplementedException();

        public bool HasUpdatedDependencies { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public string ResourceDependenciesString => throw new NotImplementedException();

        public string AllocatedToResourcesString => throw new NotImplementedException();

        public object CloneObject()
        {
            throw new NotImplementedException();
        }

        public void SetAsReadOnly()
        {
            DependentActivity.SetAsReadOnly();
        }

        public void SetAsRemovable()
        {
            DependentActivity.SetAsRemovable();
        }

        public void SetTargetResources(IEnumerable<ResourceModel> targetResources)
        {
            throw new NotImplementedException();
        }

        public void UpdateAllocatedToResources()
        {
            throw new NotImplementedException();
        }
    }
}
