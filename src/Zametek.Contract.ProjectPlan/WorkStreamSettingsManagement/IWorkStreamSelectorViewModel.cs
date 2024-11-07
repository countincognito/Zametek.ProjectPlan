using System.Collections.ObjectModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IWorkStreamSelectorViewModel
    {
        ReadOnlyObservableCollection<ISelectableWorkStreamViewModel> TargetWorkStreams { get; }

        ObservableCollection<ISelectableWorkStreamViewModel> SelectedTargetWorkStreams { get; }

        string TargetWorkStreamsString { get; }

        IList<int> SelectedWorkStreamIds { get; }

        void SetTargetWorkStreams(IEnumerable<TargetWorkStreamModel> targetWorkStreams, HashSet<int> selectedTargetWorkStreams);

        void RaiseTargetWorkStreamsPropertiesChanged();
    }
}
