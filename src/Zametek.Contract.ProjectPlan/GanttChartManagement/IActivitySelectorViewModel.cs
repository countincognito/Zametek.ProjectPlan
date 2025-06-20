using System.Collections.ObjectModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IActivitySelectorViewModel
        : IDisposable
    {
        ReadOnlyObservableCollection<ISelectableActivityViewModel> TargetActivities { get; }

        ObservableCollection<ISelectableActivityViewModel> SelectedTargetActivities { get; }

        string TargetActivitiesString { get; }

        IList<int> SelectedActivityIds { get; }

        string GetAllocatedToActivitiesString(HashSet<int> allocatedToActivities);

        void SetTargetActivities(IEnumerable<TargetActivityModel> targetActivities, HashSet<int> selectedTargetActivities);

        void SetSelectedTargetActivities(HashSet<int> selectedTargetActivities);

        void RaiseTargetActivitiesPropertiesChanged();
    }
}
