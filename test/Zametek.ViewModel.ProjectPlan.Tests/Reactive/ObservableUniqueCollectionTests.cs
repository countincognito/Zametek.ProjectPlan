using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// A sort moves only what is out of place. A selector sorts its list at each revision, nearly always with nothing to do, on
    /// the thread that asks for the revision. The UI thread reads that list while a dropdown is open, and a change of the list
    /// during a read is an error.
    /// </summary>
    public class ObservableUniqueCollectionTests
    {
        private static ObservableUniqueCollection<int> CollectionOf(params int[] values)
        {
            var collection = new ObservableUniqueCollection<int>();

            foreach (int value in values)
            {
                collection.Add(value);
            }

            return collection;
        }

        [Fact]
        public void Sort_Given_ASortedList_Then_NothingChanges()
        {
            ObservableUniqueCollection<int> collection = CollectionOf(1, 2, 3, 4);
            int changes = 0;
            collection.CollectionChanged += (_, _) => changes++;

            using (IEnumerator<int> read = collection.GetEnumerator())
            {
                read.MoveNext();
                collection.Sort(Comparer<int>.Default);

                // An enumerator throws here if the list has changed since it started.
                Should.NotThrow(() => read.MoveNext());
            }

            changes.ShouldBe(0);
            collection.ShouldBe([1, 2, 3, 4]);
        }

        [Theory]
        [InlineData(new[] { 4, 3, 2, 1 })]
        [InlineData(new[] { 2, 1, 3, 4 })]
        [InlineData(new[] { 1, 2, 4, 3 })]
        [InlineData(new[] { 4, 1, 2, 3 })]
        [InlineData(new[] { 2, 3, 4, 1 })]
        public void Sort_Given_AnUnsortedList_Then_ItIsSortedWithMoves(int[] values)
        {
            ObservableUniqueCollection<int> collection = CollectionOf(values);
            var changes = new List<NotifyCollectionChangedAction>();
            collection.CollectionChanged += (_, e) => changes.Add(e.Action);

            collection.Sort(Comparer<int>.Default);

            collection.ShouldBe([1, 2, 3, 4]);
            changes.ShouldNotBeEmpty();
            changes.ShouldAllBe(x => x == NotifyCollectionChangedAction.Move);
        }

        [Fact]
        public void Sort_Given_AComparerThatGivesAnotherOrder_Then_ItFollowsTheComparer()
        {
            ObservableUniqueCollection<int> collection = CollectionOf(1, 2, 3);

            collection.Sort(Comparer<int>.Create((x, y) => y.CompareTo(x)));

            collection.ShouldBe([3, 2, 1]);
        }

        [Fact]
        public void Sort_Given_AnEmptyListAndASingleItem_Then_NothingChanges()
        {
            ObservableUniqueCollection<int> empty = CollectionOf();
            ObservableUniqueCollection<int> single = CollectionOf(1);
            int changes = 0;
            empty.CollectionChanged += (_, _) => changes++;
            single.CollectionChanged += (_, _) => changes++;

            empty.Sort(Comparer<int>.Default);
            single.Sort(Comparer<int>.Default);

            changes.ShouldBe(0);
            single.Single().ShouldBe(1);
        }
    }
}
