using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface ITrackingManagerViewModel
        : IKillSubscriptions
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        DateTimeOffset ProjectStart { get; }

        bool ShowDates { get; }

        ReadOnlyObservableCollection<IManagedActivityViewModel> Activities { get; }

        ReadOnlyObservableCollection<IManagedResourceViewModel> Resources { get; }

        IDateTimeCalculator DateTimeCalculator { get; }

        int TrackerIndex { get; set; }

        int? PageIndex { get; set; }

        string Day00Title { get; }
        string Day01Title { get; }
        string Day02Title { get; }
        string Day03Title { get; }
        string Day04Title { get; }
        string Day05Title { get; }
        string Day06Title { get; }
        string Day07Title { get; }
        string Day08Title { get; }
        string Day09Title { get; }
        string Day10Title { get; }
        string Day11Title { get; }
        string Day12Title { get; }
        string Day13Title { get; }
        string Day14Title { get; }
        //string Day15Title { get; }
        //string Day16Title { get; }
        //string Day17Title { get; }
        //string Day18Title { get; }
        //string Day19Title { get; }
    }
}
