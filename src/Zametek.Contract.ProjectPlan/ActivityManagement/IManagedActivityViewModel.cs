using System.ComponentModel;

namespace Zametek.Contract.ProjectPlan
{
    public interface IManagedActivityViewModel
        : IDependentActivity, IDisposable, INotifyPropertyChanged, IKillSubscriptions
    {
        bool IsIsolated { get; }

        bool IsCompiled { get; }

        bool ShowDates { get; }

        bool IsUsingInfiniteResources { get; }

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

        IResourceSelectorViewModel ResourceSelector { get; }

        IWorkStreamSelectorViewModel WorkStreamSelector { get; }

        IActivityTrackerSetViewModel TrackerSet { get; }
    }
}
