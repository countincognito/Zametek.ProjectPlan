using System.Collections.ObjectModel;
using System.ComponentModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IManagedPlanViewModel
        : IDisposable, INotifyPropertyChanged, IKillSubscriptions
    {
        Guid Id { get; }

        Guid ParentId { get; set; }

        DateTimeOffset CreatedOn { get; }

        DateTimeOffset ModifiedOn { get; set; }

        string Comment { get; set; }

        ProjectPlanModel ProjectPlan { get; set; }

        ProjectPlanNodeModel Node { get; }

        bool IsLoaded { get; set; }

        ReadOnlyObservableCollection<string> Labels { get; }

        void SetLabels(IEnumerable<string> labels);

        string Name { get; }

        string Label { get; }

        ReadOnlyObservableCollection<IManagedPlanViewModel> Children { get; }

        void AddChildren(IEnumerable<IManagedPlanViewModel> managedPlans);

        void RemoveChildren(IEnumerable<Guid> managedPlans);

        void ClearChildren();
    }
}
