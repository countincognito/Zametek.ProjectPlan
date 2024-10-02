namespace Zametek.Contract.ProjectPlan
{
    public interface ISelectableResourceActivityViewModel
    {
        int Id { get; }

        string Name { get; set; }

        string DisplayName { get; }

        int PercentageWorked { get; set; }
    }
}
