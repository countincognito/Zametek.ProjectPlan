using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectFileSave
    {
        Task SaveProjectPlanFileAsync(ProjectPlanModel projectPlan, string filename);
    }
}
