namespace Zametek.Contract.ProjectPlan
{
    public interface ISelectableWorkStreamViewModel
        : IDisposable
    {
        int Id { get; }

        string Name { get; set; }

        string DisplayName { get; }

        bool IsSelected { get; set; }

        bool IsPhase { get; set; }
    }
}
