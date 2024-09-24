using System.ComponentModel;

namespace Zametek.Contract.ProjectPlan
{
    public interface IManagedActivityViewModel
        : IDependentActivity, IDisposable, INotifyPropertyChanged, IKillSubscriptions
    {
        bool IsCompiled { get; }

        bool ShowDates { get; }

        DateTimeOffset ProjectStart { get; }

        TimeSpan ProjectStartTimeOffset { get; }

        string DependenciesString { get; set; }

        string ResourceDependenciesString { get; }

        public string AllocatedToResourcesString { get; }

        DateTimeOffset? EarliestStartDateTimeOffset { get; }

        DateTimeOffset? LatestStartDateTimeOffset { get; }

        DateTimeOffset? EarliestFinishDateTimeOffset { get; }

        DateTimeOffset? LatestFinishDateTimeOffset { get; }

        DateTime? MinimumEarliestStartDateTime { get; set; }

        DateTime? MaximumLatestFinishDateTime { get; set; }

        IActivityTrackerSetViewModel TrackerSet { get; }
    }
}
