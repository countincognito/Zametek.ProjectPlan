using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class FileFilter
        : IFileFilter
    {
        public string Name { get; init; } = string.Empty;

        public List<string> Patterns { get; init; } = [];
    }
}
