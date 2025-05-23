﻿using System.ComponentModel;

namespace Zametek.Contract.ProjectPlan
{
    public interface IManagedActivityViewModel
        : IDependentActivity, IDisposable, INotifyPropertyChanged, IKillSubscriptions
    {
        bool IsIsolated { get; }

        bool IsCompiled { get; }

        bool ShowDates { get; }

        bool HasResources { get; }

        bool HasWorkStreams { get; }

        DateTimeOffset ProjectStart { get; }

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
