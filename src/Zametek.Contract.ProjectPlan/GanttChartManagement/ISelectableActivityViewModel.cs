namespace Zametek.Contract.ProjectPlan
{
    public interface ISelectableActivityViewModel
    {
        int Id { get; }

        string Name { get; set; }

        string DisplayName { get; }
    }
}
