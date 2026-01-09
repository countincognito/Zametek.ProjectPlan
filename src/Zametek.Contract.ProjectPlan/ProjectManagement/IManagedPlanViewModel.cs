using System.Collections.ObjectModel;
using System.ComponentModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IManagedPlanViewModel
        : IDisposable, INotifyPropertyChanged, IKillSubscriptions
    {
        Guid Id { get; }

        Guid ParentId { get; }

        string Comment { get; }

        ReadOnlyObservableCollection<string> Labels { get; }

        void SetLabels(IEnumerable<string> labels);

        string Label { get; }

        ProjectPlanModel ProjectPlan { get; }

        ReadOnlyObservableCollection<IManagedPlanViewModel> Children { get; }

        void AddChildren(IEnumerable<IManagedPlanViewModel> managedPlans);

        void RemoveChildren(IEnumerable<Guid> managedPlans);

        void ClearChildren();

        //bool CanBeRemoved { get; }

        //public void SetAsReadOnly();

        //public void SetAsRemovable();
    }
}
