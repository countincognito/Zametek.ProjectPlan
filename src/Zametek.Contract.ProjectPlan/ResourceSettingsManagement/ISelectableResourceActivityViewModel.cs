namespace Zametek.Contract.ProjectPlan
{
    public interface ISelectableResourceActivityViewModel
        : IDisposable
    {
        int Id { get; }

        string Name { get; set; }

        string DisplayName { get; }

        bool IsSelected { get; set; }
    }
}
