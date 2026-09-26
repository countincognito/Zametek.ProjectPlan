using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectFileSave
    {
        // Writes the project to the stream. The caller owns the stream, which is left open.
        Task SaveProjectFileAsync(ProjectModel project, Stream stream);
    }
}
