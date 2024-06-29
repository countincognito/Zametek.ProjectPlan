namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record WorkStreamSettingsModel
    {
        public List<WorkStreamModel> WorkStreams { get; init; } = [];
    }
}
