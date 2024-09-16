using System.Collections.ObjectModel;

namespace Zametek.Contract.ProjectPlan
{
    public interface ITrackingManagerViewModel
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        DateTimeOffset ProjectStart { get; }

        bool ShowDates { get; }

        ReadOnlyObservableCollection<IManagedActivityViewModel> Activities { get; }

        IDateTimeCalculator DateTimeCalculator { get; }

        int TrackerIndex { get; set; }

        string Day00Title { get; }
        string Day01Title { get; }
        string Day02Title { get; }
        string Day03Title { get; }
        string Day04Title { get; }
    }
}
