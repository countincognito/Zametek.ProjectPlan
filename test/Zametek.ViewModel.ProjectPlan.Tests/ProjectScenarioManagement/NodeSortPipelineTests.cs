using DynamicData;
using DynamicData.Binding;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Subjects;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Pins the DynamicData operator chain the scenario tree relies on
    /// (AutoRefresh -> Sort with an observable comparer -> Bind): incremental
    /// adds must land at their sorted positions, including after a comparer
    /// change. The paste flow additionally finishes with a full re-sort
    /// emission because the tree VIEW does not reliably present mid-list
    /// insertions until a reorder pass; these tests document that the data
    /// pipeline itself is not the culprit, guarding against package
    /// regressions changing that.
    /// </summary>
    public class NodeSortPipelineTests
    {
        private sealed class Item : INotifyPropertyChanged
        {
            private string m_Name = string.Empty;
            private DateTimeOffset m_CreatedOn;
            private DateTimeOffset m_ModifiedOn;

            public event PropertyChangedEventHandler? PropertyChanged;

            public string Name
            {
                get => m_Name;
                set { m_Name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); }
            }

            public DateTimeOffset CreatedOn
            {
                get => m_CreatedOn;
                set { m_CreatedOn = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CreatedOn))); }
            }

            public DateTimeOffset ModifiedOn
            {
                get => m_ModifiedOn;
                set { m_ModifiedOn = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModifiedOn))); }
            }
        }

        private static Item MakeItem(string name, int createdDay)
        {
            return new Item
            {
                Name = name,
                CreatedOn = new DateTimeOffset(2026, 1, createdDay, 0, 0, 0, TimeSpan.Zero),
                ModifiedOn = new DateTimeOffset(2026, 2, createdDay, 0, 0, 0, TimeSpan.Zero),
            };
        }

        [Fact]
        public void Sort_Given_LateAdds_Then_PlacedAtSortedPosition()
        {
            var comparer = new BehaviorSubject<IComparer<Item>>(
                SortExpressionComparer<Item>.Ascending(x => x.CreatedOn));
            var source = new SourceList<Item>();

            using IDisposable sub = source.Connect()
                .AutoRefresh(x => x.Name)
                .AutoRefresh(x => x.CreatedOn)
                .AutoRefresh(x => x.ModifiedOn)
                .Sort(comparer)
                .Bind(out ReadOnlyObservableCollection<Item> view)
                .Subscribe();

            // Seed two items, then add one that belongs BETWEEN them.
            source.Add(MakeItem(@"day1", 1));
            source.Add(MakeItem(@"day9", 9));
            source.Add(MakeItem(@"day5", 5));

            view.Select(x => x.Name).ShouldBe([@"day1", @"day5", @"day9"]);
        }

        [Fact]
        public void Sort_Given_LateAddsAfterComparerChange_Then_PlacedAtSortedPosition()
        {
            var comparer = new BehaviorSubject<IComparer<Item>>(
                SortExpressionComparer<Item>.Ascending(x => x.CreatedOn));
            var source = new SourceList<Item>();

            using IDisposable sub = source.Connect()
                .AutoRefresh(x => x.Name)
                .AutoRefresh(x => x.CreatedOn)
                .AutoRefresh(x => x.ModifiedOn)
                .Sort(comparer)
                .Bind(out ReadOnlyObservableCollection<Item> view)
                .Subscribe();

            source.Add(MakeItem(@"charlie", 1));
            source.Add(MakeItem(@"alpha", 9));

            // Switch to descending-by-name, then add an item that belongs in
            // the middle under the NEW comparer.
            comparer.OnNext(SortExpressionComparer<Item>.Descending(x => x.Name));
            source.Add(MakeItem(@"bravo", 5));

            view.Select(x => x.Name).ShouldBe([@"charlie", @"bravo", @"alpha"]);
        }
    }
}
