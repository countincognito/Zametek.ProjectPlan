using System.Collections.ObjectModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IResourceSelectorViewModel
        : IDisposable
    {
        ReadOnlyObservableCollection<ISelectableResourceViewModel> TargetResources { get; }

        ObservableCollection<ISelectableResourceViewModel> SelectedTargetResources { get; }

        string TargetResourcesString { get; }

        IList<int> SelectedResourceIds { get; }

        string GetAllocatedToResourcesString(HashSet<int> allocatedToResources);

        void SetTargetResources(IEnumerable<ResourceModel> targetResources, HashSet<int> selectedTargetResources);

        void ClearTargetResources();

        void ClearSelectedTargetResources();

        void RaiseTargetResourcesPropertiesChanged();
    }
}
