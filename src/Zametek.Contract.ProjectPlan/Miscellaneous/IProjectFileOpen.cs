using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectFileOpen
    {
        Task<ProjectPlanModel> OpenProjectPlanFileAsync(string filename);
    }
}
