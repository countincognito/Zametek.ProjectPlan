namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ResourceSeriesSetModel
    {
        public List<ResourceScheduleModel> ResourceSchedules { get; init; } = [];

        public List<ResourceSeriesModel> Scheduled { get; init; } = [];

        public List<ResourceSeriesModel> Unscheduled { get; init; } = [];

        public List<ResourceSeriesModel> Combined { get; init; } = [];
    }
}
