using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IPlanManagerViewModel
        : IKillSubscriptions
    {
        string ProjectTitle { get; }

        bool IsBusy { get; }

        bool IsProjectUpdated { get; }

        bool HasStaleOutputs { get; }

        void ResetProject();

        void ProcessProject(ProjectModel projectModel);

        ProjectModel BuildProject();
    }
}
