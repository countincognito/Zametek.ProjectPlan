using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ResourceSeriesModel
    {
        public string Title { get; init; } = string.Empty;

        public InterActivityAllocationType InterActivityAllocationType { get; init; }

        public ResourceScheduleModel ResourceSchedule { get; init; } = new ResourceScheduleModel();

        public ColorFormatModel ColorFormat { get; init; } = new ColorFormatModel();

        public double UnitCost { get; init; }

        public int DisplayOrder { get; init; }
    }
}
