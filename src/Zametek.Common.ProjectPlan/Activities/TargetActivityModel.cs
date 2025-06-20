namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record TargetActivityModel
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;
    }
}
