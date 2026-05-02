namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record DataGridModel
    {
        public string Name { get; init; } = string.Empty;

        public List<DataGridColumnModel> Columns { get; init; } = [];
    }
}
