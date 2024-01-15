namespace Zametek.Contract.ProjectPlan
{
    public interface IFileFilter
    {
        string Name { get; init; }

        List<string> Patterns { get; init; }
    }
}