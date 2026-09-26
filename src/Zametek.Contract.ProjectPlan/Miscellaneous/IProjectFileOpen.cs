using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IProjectFileOpen
    {
        // Reads a project from the stream, from its current position to its end. The caller owns the stream, which is
        // left open.
        Task<ProjectModel> OpenProjectFileAsync(Stream stream);
    }
}
