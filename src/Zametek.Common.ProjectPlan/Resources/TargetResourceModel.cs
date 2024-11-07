namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record TargetResourceModel
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;
    }
}
