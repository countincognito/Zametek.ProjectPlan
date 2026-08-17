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
    /// adds must land at their sorted positions - including after comparer
    /// changes, for freshly subscribed pipelines, and through the
    /// clear-and-re-add reload the paste flow performs. Ties are the
    /// load-bearing subtlety: a batch paste mints one timestamp for the whole
    /// clone batch, and with a comparer that has no tie-breaker the pipeline
    /// legitimately leaves tied items in arbitrary, operation-dependent order
    /// (inserts append them in arrival order, stable re-sorts preserve
    /// whatever arrangement preceded) - which is how "pasted nodes appear
    /// unsorted" happened while every distinct-key test passed. The
    /// production comparers therefore always carry a secondary key
    /// (ProjectScenarioNodeHelper.BuildSortComparer); the tied-key test here
    /// documents that such a comparer is sufficient for the pipeline to
    /// settle ties deterministically.
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

        [Fact]
        public void Sort_Given_FreshSubscriberAfterPriorComparerPushes_Then_RawOrderAddsLandSorted()
        {
            // Replicates a project reopen: the comparer subject has already
            // been pushed several times when a brand-new node view model
            // subscribes its children pipeline, and the children then arrive
            // one edit at a time in raw file order.
            var comparer = new BehaviorSubject<IComparer<Item>>(
                SortExpressionComparer<Item>.Ascending(x => x.CreatedOn));
            comparer.OnNext(SortExpressionComparer<Item>.Ascending(x => x.Name));
            comparer.OnNext(SortExpressionComparer<Item>.Ascending(x => x.CreatedOn));
            comparer.OnNext(SortExpressionComparer<Item>.Ascending(x => x.CreatedOn));

            var source = new SourceList<Item>();

            using IDisposable sub = source.Connect()
                .AutoRefresh(x => x.Name)
                .AutoRefresh(x => x.CreatedOn)
                .AutoRefresh(x => x.ModifiedOn)
                .Sort(comparer)
                .Bind(out ReadOnlyObservableCollection<Item> view)
                .Subscribe();

            foreach (int day in (int[])[7, 9, 6, 4, 8, 5, 10])
            {
                source.Edit(list => list.Add(MakeItem(@$"day{day}", day)));
            }

            view.Select(x => x.Name).ShouldBe(
                [@"day4", @"day5", @"day6", @"day7", @"day8", @"day9", @"day10"]);
        }

        [Fact]
        public void Sort_Given_ClearAndReAddReload_Then_ViewIsSorted()
        {
            // Replicates ReloadSiblings/ReloadChildren: a single edit that
            // clears the source and re-adds the same instances in the source
            // list's raw insertion order.
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

            foreach (int day in (int[])[7, 9, 6, 4, 8, 5, 10])
            {
                source.Edit(list => list.Add(MakeItem(@$"day{day}", day)));
            }

            source.Edit(list =>
            {
                List<Item> reloaded = [.. list];
                list.Clear();
                list.AddRange(reloaded);
            });

            view.Select(x => x.Name).ShouldBe(
                [@"day4", @"day5", @"day6", @"day7", @"day8", @"day9", @"day10"]);
        }

        [Fact]
        public void Sort_Given_AddThatTiesOnSortKey_Then_LandsAdjacentToTiedItem()
        {
            // Replicates a cut-paste clone that inherits its source's exact
            // CreatedOn: the new item ties with an existing one and must land
            // adjacent to it, not at some unrelated position.
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

            foreach (int day in (int[])[4, 5, 6, 7, 8, 9, 10])
            {
                source.Edit(list => list.Add(MakeItem(@$"day{day}", day)));
            }

            Item original = source.Items.Single(x => x.Name == @"day6");
            var clone = new Item
            {
                Name = @"day6-1",
                CreatedOn = original.CreatedOn,
                ModifiedOn = original.ModifiedOn,
            };
            source.Edit(list => list.Add(clone));

            int cloneIndex = view.Select(x => x.Name).ToList().IndexOf(@"day6-1");
            cloneIndex.ShouldBeInRange(2, 3);
        }

        [Fact]
        public void Sort_Given_TiedPrimaryKeysWithTieBreakerComparer_Then_TiesSettleDeterministically()
        {
            // Replicates a batch paste, where every clone carries the same
            // timestamp: with a tie-breaker in the comparer (matching
            // ProjectScenarioNodeHelper.BuildSortComparer), tied items must
            // order by name on insert AND keep that order through the
            // clear-and-re-add reload the paste flow finishes with.
            var sharedTime = new DateTimeOffset(2026, 8, 16, 17, 38, 32, TimeSpan.Zero);
            var comparer = new BehaviorSubject<IComparer<Item>>(
                SortExpressionComparer<Item>
                    .Ascending(x => x.CreatedOn)
                    .ThenByAscending(x => x.Name));
            var source = new SourceList<Item>();

            using IDisposable sub = source.Connect()
                .AutoRefresh(x => x.Name)
                .AutoRefresh(x => x.CreatedOn)
                .AutoRefresh(x => x.ModifiedOn)
                .Sort(comparer)
                .Bind(out ReadOnlyObservableCollection<Item> view)
                .Subscribe();

            foreach (string name in (string[])[@"charlie", @"alpha", @"delta", @"bravo"])
            {
                source.Edit(list => list.Add(new Item
                {
                    Name = name,
                    CreatedOn = sharedTime,
                    ModifiedOn = sharedTime,
                }));
            }

            view.Select(x => x.Name).ShouldBe([@"alpha", @"bravo", @"charlie", @"delta"]);

            source.Edit(list =>
            {
                List<Item> reloaded = [.. list];
                list.Clear();
                list.AddRange(reloaded);
            });

            view.Select(x => x.Name).ShouldBe([@"alpha", @"bravo", @"charlie", @"delta"]);
        }
    }
}
