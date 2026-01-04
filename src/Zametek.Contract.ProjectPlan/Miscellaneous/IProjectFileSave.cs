using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectFileSave
    {
        Task SaveProjectFileAsync(ProjectModel project, string filename);
    }
}
