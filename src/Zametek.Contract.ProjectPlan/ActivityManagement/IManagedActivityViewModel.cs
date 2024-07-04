using System.Collections.ObjectModel;
using System.ComponentModel;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface IManagedActivityViewModel
        : IDependentActivity<int, int, int>, IDisposable, INotifyPropertyChanged
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

        ObservableCollection<ITrackerViewModel> Trackers { get; }

        void AddTracker();

        void RemoveTracker();
    }
}
