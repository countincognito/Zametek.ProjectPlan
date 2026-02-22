using System.Collections.ObjectModel;
using System.ComponentModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IManagedNodeViewModel
        : IDisposable, INotifyPropertyChanged, IKillSubscriptions
    {
        Guid Id { get; }

        Guid ParentId { get; set; }

        public bool IsFolder { get; }

        string Name { get; set; }

        DateTimeOffset CreatedOn { get; }

        DateTimeOffset ModifiedOn { get; set; }

        ProjectScenarioModel? Scenario { get; set; }

        ProjectScenarioNodeModel Node { get; }

        ProjectScenarioFileModel File { get; }

        public bool IsTracked { get; set; }

        bool IsLoaded { get; set; }

        IReadOnlyList<string> RawLabels { get; }

        ReadOnlyObservableCollection<string> Labels { get; }

        void SetLabels(IEnumerable<string> labels);

        string Label { get; }

        string DisplayName { get; }

        IReadOnlyList<IManagedNodeViewModel> RawChildren { get; }

        ReadOnlyObservableCollection<IManagedNodeViewModel> Children { get; }

        void AddChildren(IEnumerable<IManagedNodeViewModel> managedNodes);

        void RemoveChildren(IEnumerable<Guid> managedNodeIds);

        void ClearChildren();
    }
}
