namespace Zametek.Contract.ProjectPlan
{
    public interface ISelectableWorkStreamViewModel
    {
        int Id { get; }

        string Name { get; set; }

        string DisplayName { get; }

        bool IsPhase { get; set; }
    }
}
