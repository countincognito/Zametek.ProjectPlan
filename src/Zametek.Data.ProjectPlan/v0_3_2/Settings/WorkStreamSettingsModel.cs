namespace Zametek.Data.ProjectPlan.v0_3_2
{
    [Serializable]
    public record WorkStreamSettingsModel
    {
        public List<WorkStreamModel> WorkStreams { get; init; } = [];
    }
}
