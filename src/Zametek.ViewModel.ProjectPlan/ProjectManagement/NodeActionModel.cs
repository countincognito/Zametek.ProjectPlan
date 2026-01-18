namespace Zametek.ViewModel.ProjectPlan
{
    [Serializable]
    public record NodeActionModel
    {
        public List<Guid> NodeIds { get; init; } = [];

        public NodeAction Action { get; set; } = default;
    }
}
