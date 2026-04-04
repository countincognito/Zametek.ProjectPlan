namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record HolidaySettingsModel
    {
        public List<HolidayModel> Holidays { get; init; } = [];
    }
}
