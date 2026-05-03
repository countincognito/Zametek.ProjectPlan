namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record TrackingSeriesSetFilterModel
    {
        public List<int> SelectedResourceIds { get; init; } = [];
    }
}
