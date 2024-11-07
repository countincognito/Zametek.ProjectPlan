namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record TargetWorkStreamModel
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public bool IsPhase { get; init; }
    }
}
