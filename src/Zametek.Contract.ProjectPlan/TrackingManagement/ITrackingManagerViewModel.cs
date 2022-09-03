using System.Collections.ObjectModel;
using System.Windows.Input;

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

        string StartTime { get; }

        string EndTime { get; }

        int? StartColumnIndex { get; set; }

        int ColumnsShown { get; set; }

        int? EndColumnIndex { get; }

        int TrackerCount { get; }

        ReadOnlyObservableCollection<IColumnSelectorViewModel> AvailableStartColumns { get; }

        IColumnSelectorViewModel? StartColumnSelector { get; set; }

        //ReadOnlyObservableCollection<IColumnCountViewModel> AvailableColumnsShown { get; }

        //IColumnCountViewModel? ColumnsShownSelector { get; set; }

        ICommand AddTrackersCommand { get; }

        ICommand RemoveTrackersCommand { get; }
    }
}
