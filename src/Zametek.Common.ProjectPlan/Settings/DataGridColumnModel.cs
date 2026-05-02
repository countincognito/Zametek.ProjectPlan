namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record DataGridColumnModel
    {
        public string Name { get; init; } = string.Empty;

        public int PositionIndex { get; init; } = 0;

        public int DisplayIndex { get; init; } = 0;
    }
}
