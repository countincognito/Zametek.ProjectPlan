namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record SetByIntModel
    {
        public string Name { get; init; } = string.Empty;

        public int Content { get; init; } = 0;
    }
}
