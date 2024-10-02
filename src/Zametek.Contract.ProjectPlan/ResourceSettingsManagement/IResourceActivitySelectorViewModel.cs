using System.Collections.ObjectModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IResourceActivitySelectorViewModel
        : IDisposable
    {
        int ResourceId { get; }

        int Time { get; }

        ReadOnlyObservableCollection<ISelectableResourceActivityViewModel> TargetResourceActivities { get; }

        ObservableCollection<ISelectableResourceActivityViewModel> SelectedTargetResourceActivities { get; }

        string? TargetResourceActivitiesString { get; }

        IList<int> SelectedResourceActivityIds { get; }

        void SetTargetResourceActivities(IEnumerable<ResourceActivityTrackerModel> targetResourceActivities, HashSet<int> selectedTargetResourceActivities);

        void RaiseTargetResourceActivitiesPropertiesChanged();
    }
}
