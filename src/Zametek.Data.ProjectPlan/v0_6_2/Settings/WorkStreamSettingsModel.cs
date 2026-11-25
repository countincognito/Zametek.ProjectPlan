namespace Zametek.Data.ProjectPlan.v0_6_2
{
    // The work stream settings of v0.3.2, with the work streams of v0.6.2.
    [Serializable]
    public record WorkStreamSettingsModel
    {
        public List<WorkStreamModel> WorkStreams { get; init; } = [];
    }
}
