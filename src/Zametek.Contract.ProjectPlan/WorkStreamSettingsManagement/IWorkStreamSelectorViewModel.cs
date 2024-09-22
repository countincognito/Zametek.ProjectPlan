using System.Collections.ObjectModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IWorkStreamSelectorViewModel
        : IDisposable
    {
        ReadOnlyObservableCollection<ISelectableWorkStreamViewModel> TargetWorkStreams { get; }

        ObservableCollection<ISelectableWorkStreamViewModel> SelectedTargetWorkStreams { get; }

        string TargetWorkStreamsString { get; }

        IList<int> SelectedWorkStreamIds { get; }

        void SetTargetWorkStreams(IEnumerable<WorkStreamModel> targetWorkStreams, HashSet<int> selectedTargetWorkStreams);

        void ClearTargetWorkStreams();

        void ClearSelectedTargetWorkStreams();

        void RaiseTargetWorkStreamsPropertiesChanged();
    }
}
