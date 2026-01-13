using System.Collections.ObjectModel;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectManagerViewModel
        : IKillSubscriptions
    {
        bool IsBusy { get; }

        bool IsProjectUpdated { get; set; }

        bool IsLoading { get; }

        bool IsBranching { get; }

        bool IsSpawning { get; }

        bool ProjectHasChanges { get; }

        IManagedPlanViewModel Root { get; }

        ReadOnlyObservableCollection<IManagedPlanViewModel> Plans { get; }

        ObservableCollection<IManagedPlanViewModel> SelectedPlans { get; }

        IManagedPlanViewModel? SelectedPlan { get; }

        ICommand SetSelectedManagedPlansCommand { get; }

        ICommand LoadProjectPlanFileCommand { get; }

        ICommand BranchProjectPlanFileCommand { get; }

        ICommand SpawnProjectPlanFileCommand { get; }

        void ResetProject();

        void ProcessProject(ProjectModel projectModel);

        IManagedPlanViewModel? GetProjectPlan(Guid projectPlanId);

        IManagedPlanViewModel? GetProjectPlanParent(Guid projectPlanId);

        ProjectModel BuildProject();
    }
}
