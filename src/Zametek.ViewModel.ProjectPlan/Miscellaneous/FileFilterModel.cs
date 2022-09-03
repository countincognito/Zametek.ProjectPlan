using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class FileFilter
        : IFileFilter
    {
        public string? Name { get; init; }

        public List<string> Extensions { get; init; } = new List<string>();
    }
}
