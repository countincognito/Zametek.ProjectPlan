namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ResourceSeriesSetModel
    {
        public List<ResourceScheduleModel> ResourceSchedules { get; init; } = new List<ResourceScheduleModel>();

        public List<ResourceSeriesModel> Scheduled { get; init; } = new List<ResourceSeriesModel>();

        public List<ResourceSeriesModel> Unscheduled { get; init; } = new List<ResourceSeriesModel>();

        public List<ResourceSeriesModel> Combined { get; init; } = new List<ResourceSeriesModel>();
    }
}
