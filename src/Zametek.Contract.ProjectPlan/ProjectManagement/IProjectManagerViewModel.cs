using System.Collections.ObjectModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectManagerViewModel
        : IKillSubscriptions
    {
        bool IsBusy { get; }

        IManagedPlanViewModel Root { get; }

        ReadOnlyObservableCollection<IManagedPlanViewModel> Plans { get; }

        void ResetProject();

        void ProcessProject(ProjectModel projectModel);

        IManagedPlanViewModel? GetProjectPlan(Guid projectPlanId);

        IManagedPlanViewModel? GetProjectPlanParent(Guid projectPlanId);

        ProjectModel BuildProject();

        void AddManagedPlans(IEnumerable<ProjectPlanNodeModel> projectPlanNodeModels);
    }
}
