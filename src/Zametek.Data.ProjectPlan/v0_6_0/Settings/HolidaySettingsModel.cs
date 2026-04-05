namespace Zametek.Data.ProjectPlan.v0_6_0
{
    [Serializable]
    public record HolidaySettingsModel
    {
        public List<HolidayModel> Holidays { get; init; } = [];
    }
}
