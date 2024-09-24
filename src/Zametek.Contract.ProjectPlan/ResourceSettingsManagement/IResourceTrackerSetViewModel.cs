using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IResourceTrackerSetViewModel
        : IDisposable
    {
        List<ResourceTrackerModel> Trackers { get; }

        int ResourceId { get; }

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
        IResourceActivitySelectorViewModel Day15 { get; }
        IResourceActivitySelectorViewModel Day16 { get; }
        IResourceActivitySelectorViewModel Day17 { get; }
        IResourceActivitySelectorViewModel Day18 { get; }
        IResourceActivitySelectorViewModel Day19 { get; }
    }
}
