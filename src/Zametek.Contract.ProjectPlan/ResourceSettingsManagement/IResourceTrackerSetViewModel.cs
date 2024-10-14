using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IResourceTrackerSetViewModel
        : IDisposable
    {
        List<ResourceTrackerModel> Trackers { get; }

        int ResourceId { get; }

        int? LastTrackerIndex { get; }

        ICommand SetTrackerIndexCommand { get; }

        string SearchSymbol { get; }

        void RefreshIndex();

        IResourceActivitySelectorViewModel Day00 { get; }
        IResourceActivitySelectorViewModel Day01 { get; }
        IResourceActivitySelectorViewModel Day02 { get; }
        IResourceActivitySelectorViewModel Day03 { get; }
        IResourceActivitySelectorViewModel Day04 { get; }
        IResourceActivitySelectorViewModel Day05 { get; }
        IResourceActivitySelectorViewModel Day06 { get; }
        IResourceActivitySelectorViewModel Day07 { get; }
        IResourceActivitySelectorViewModel Day08 { get; }
        IResourceActivitySelectorViewModel Day09 { get; }
        IResourceActivitySelectorViewModel Day10 { get; }
        IResourceActivitySelectorViewModel Day11 { get; }
        IResourceActivitySelectorViewModel Day12 { get; }
        IResourceActivitySelectorViewModel Day13 { get; }
        IResourceActivitySelectorViewModel Day14 { get; }
    }
}
