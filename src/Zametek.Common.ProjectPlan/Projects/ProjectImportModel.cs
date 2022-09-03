namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ProjectImportModel
    {
        public DateTimeOffset ProjectStart { get; init; }

        public List<DependentActivityModel> DependentActivities { get; init; } = new List<DependentActivityModel>();

        public List<ResourceModel> Resources { get; init; } = new List<ResourceModel>();
    }
}
