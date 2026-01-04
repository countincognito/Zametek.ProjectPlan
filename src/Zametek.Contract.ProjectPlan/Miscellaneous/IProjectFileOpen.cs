using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectFileOpen
    {
        Task<ProjectModel> OpenProjectFileAsync(string filename);
    }
}
