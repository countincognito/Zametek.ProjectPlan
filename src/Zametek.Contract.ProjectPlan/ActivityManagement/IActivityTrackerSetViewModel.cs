using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IActivityTrackerSetViewModel
        : IDisposable
    {
        List<ActivityTrackerModel> Trackers { get; }

        int ActivityId { get; }

        int? LastTrackerIndex { get; }

        int? LastTrackerValue { get; }

        ICommand SetTrackerIndexCommand { get; }

        string SearchSymbol { get; }

        void RefreshIndex();

        int? Day00 { get; set; }
        int? Day01 { get; set; }
        int? Day02 { get; set; }
        int? Day03 { get; set; }
        int? Day04 { get; set; }
        int? Day05 { get; set; }
        int? Day06 { get; set; }
        int? Day07 { get; set; }
        int? Day08 { get; set; }
        int? Day09 { get; set; }
        int? Day10 { get; set; }
        int? Day11 { get; set; }
        int? Day12 { get; set; }
        int? Day13 { get; set; }
        int? Day14 { get; set; }
    }
}
