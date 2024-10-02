namespace Zametek.Contract.ProjectPlan
{
    public interface ISelectableResourceViewModel
    {
        int Id { get; }

        string Name { get; set; }

        string DisplayName { get; }
    }
}
