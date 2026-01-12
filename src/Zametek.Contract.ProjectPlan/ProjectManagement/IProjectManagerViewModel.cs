using System.Collections.ObjectModel;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectManagerViewModel
        : IKillSubscriptions
    {
        bool IsBusy { get; }

        bool IsLoading { get; }

        bool IsBranching { get; }

        bool IsSpawning { get; }

        IManagedPlanViewModel Root { get; }

        ReadOnlyObservableCollection<IManagedPlanViewModel> Plans { get; }

        ObservableCollection<IManagedPlanViewModel> SelectedPlans { get; }

        ICommand LoadProjectPlanFileCommand { get; }

        ICommand BranchProjectPlanFileCommand { get; }

        ICommand SpawnProjectPlanFileCommand { get; }

        void ResetProject();

        void ProcessProject(ProjectModel projectModel);

        IManagedPlanViewModel? GetProjectPlan(Guid projectPlanId);

        IManagedPlanViewModel? GetProjectPlanParent(Guid projectPlanId);

        ProjectModel BuildProject();

        void AddManagedPlans(IEnumerable<ProjectPlanNodeModel> projectPlanNodeModels);
    }
}
