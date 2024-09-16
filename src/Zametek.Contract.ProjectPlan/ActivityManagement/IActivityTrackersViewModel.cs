namespace Zametek.Contract.ProjectPlan
{
    public interface IActivityTrackersViewModel
        : IDisposable
    {
        int ActivityId { get; }

        int? Day00 { get; set; }
        int? Day01 { get; set; }
        int? Day02 { get; set; }
        int? Day03 { get; set; }
        int? Day04 { get; set; }
    }
}
