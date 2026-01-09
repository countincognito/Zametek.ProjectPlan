using System.Collections.ObjectModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectManagerViewModel
        : IKillSubscriptions
    {
        IManagedPlanViewModel? Root { get; }

        ReadOnlyObservableCollection<IManagedPlanViewModel> Plans { get; }

        void ResetProject();

        void ProcessProject(ProjectModel projectModel);

        ProjectModel BuildProject();

        void AddManagedPlans(IEnumerable<ProjectPlanNodeModel> projectPlanNodeModels);
    }
}
