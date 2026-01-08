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

        ProjectPlanModel ProjectPlan { get; }

        ReadOnlyObservableCollection<IManagedPlanViewModel> Children { get; }

        bool CanBeRemoved { get; }

        public void SetAsReadOnly();

        public void SetAsRemovable();
    }
}
