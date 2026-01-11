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

        IManagedPlanViewModel Root { get; }

        ReadOnlyObservableCollection<IManagedPlanViewModel> Plans { get; }

        ObservableCollection<IManagedPlanViewModel> SelectedPlans { get; }

        ICommand LoadProjectPlanFileCommand { get; }

        ICommand SpawnProjectPlanFileCommand { get; }

        ICommand BranchProjectPlanFileCommand { get; }

        void ResetProject();

        void ProcessProject(ProjectModel projectModel);

        IManagedPlanViewModel? GetProjectPlan(Guid projectPlanId);

        IManagedPlanViewModel? GetProjectPlanParent(Guid projectPlanId);

        ProjectModel BuildProject();

        void AddManagedPlans(IEnumerable<ProjectPlanNodeModel> projectPlanNodeModels);

        Task LoadProjectPlanFileAsync();

        Task SpawnProjectPlanFileAsync();

        Task BranchProjectPlanFileAsync();
    }
}
