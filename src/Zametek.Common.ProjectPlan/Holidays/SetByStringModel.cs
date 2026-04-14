namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record SetByStringModel
    {
        public string Name { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;
    }
}
