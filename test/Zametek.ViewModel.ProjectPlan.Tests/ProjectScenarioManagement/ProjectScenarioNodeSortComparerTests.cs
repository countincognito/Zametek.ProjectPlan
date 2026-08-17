using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using SortDirection = Zametek.Common.ProjectPlan.SortDirection;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Unit tests for ProjectScenarioNodeHelper.BuildSortComparer: every mode
    /// and direction must produce a deterministic total order even when the
    /// primary keys tie, because batch operations (multi-select paste,
    /// recursive folder clones) mint a single timestamp for every node they
    /// create. Without the tie-breaker, tied items settle in arbitrary,
    /// operation-dependent order - the root cause of the "pasted nodes appear
    /// unsorted" defect.
    /// </summary>
    public class ProjectScenarioNodeSortComparerTests
    {
        private static readonly DateTimeOffset s_EarlierTime = new(2026, 8, 16, 17, 38, 31, TimeSpan.Zero);
        private static readonly DateTimeOffset s_SharedTime = new(2026, 8, 16, 17, 38, 32, TimeSpan.Zero);
        private static readonly DateTimeOffset s_LaterTime = new(2026, 8, 16, 17, 38, 33, TimeSpan.Zero);

        /// <summary>
        /// Only the three sort keys are real; everything else throws so a
        /// comparer reaching beyond Name/CreatedOn/ModifiedOn fails loudly.
        /// </summary>
        private sealed class NodeStub : IManagedNodeViewModel
        {
            public required string Name { get; set; }

            public DateTimeOffset CreatedOn { get; init; }

            public DateTimeOffset ModifiedOn { get; set; }

            public Guid Id => throw new NotSupportedException();
            public Guid ParentId { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public bool IsFolder => throw new NotSupportedException();
            public ProjectScenarioModel? Scenario { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public ProjectScenarioNodeModel Node => throw new NotSupportedException();
            public ProjectScenarioFileModel File => throw new NotSupportedException();
            public bool IsTracked { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public bool IsUpdated { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public bool IsLoaded { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public IReadOnlyList<string> RawLabels => throw new NotSupportedException();
            public ReadOnlyObservableCollection<string> Labels => throw new NotSupportedException();
            public string Label => throw new NotSupportedException();
            public string DisplayName => throw new NotSupportedException();
            public IReadOnlyList<IManagedNodeViewModel> RawChildren => throw new NotSupportedException();
            public ReadOnlyObservableCollection<IManagedNodeViewModel> Children => throw new NotSupportedException();

            public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }

            public void SetLabels(IEnumerable<string> labels) => throw new NotSupportedException();
            public void AddChildren(IEnumerable<IManagedNodeViewModel> managedNodes) => throw new NotSupportedException();
            public void RemoveChildren(IEnumerable<Guid> managedNodeIds) => throw new NotSupportedException();
            public void ClearChildren() => throw new NotSupportedException();
            public void ReloadChildren() => throw new NotSupportedException();
            public void KillSubscriptions() => throw new NotSupportedException();
            public void Dispose() => throw new NotSupportedException();
        }

        private static NodeStub MakeNode(
            string name,
            DateTimeOffset createdOn,
            DateTimeOffset? modifiedOn = null)
        {
            return new NodeStub
            {
                Name = name,
                CreatedOn = createdOn,
                ModifiedOn = modifiedOn ?? createdOn,
            };
        }

        [Fact]
        public void BuildSortComparer_Given_CreatedOnAscendingWithTiedTimestamps_Then_TiesOrderedByNameAscending()
        {
            List<IManagedNodeViewModel> nodes =
            [
                MakeNode(@"Charlie", s_SharedTime),
                MakeNode(@"Delta", s_LaterTime),
                MakeNode(@"Alpha", s_SharedTime),
                MakeNode(@"Zulu", s_EarlierTime),
                MakeNode(@"Bravo", s_SharedTime),
            ];

            nodes.Sort(ProjectScenarioNodeHelper.BuildSortComparer(SortMode.CreatedOn, SortDirection.Ascending));

            nodes.Select(x => x.Name).ShouldBe([@"Zulu", @"Alpha", @"Bravo", @"Charlie", @"Delta"]);
        }

        [Fact]
        public void BuildSortComparer_Given_CreatedOnDescendingWithTiedTimestamps_Then_TiesOrderedByNameDescending()
        {
            List<IManagedNodeViewModel> nodes =
            [
                MakeNode(@"Charlie", s_SharedTime),
                MakeNode(@"Delta", s_LaterTime),
                MakeNode(@"Alpha", s_SharedTime),
                MakeNode(@"Zulu", s_EarlierTime),
                MakeNode(@"Bravo", s_SharedTime),
            ];

            nodes.Sort(ProjectScenarioNodeHelper.BuildSortComparer(SortMode.CreatedOn, SortDirection.Descending));

            nodes.Select(x => x.Name).ShouldBe([@"Delta", @"Charlie", @"Bravo", @"Alpha", @"Zulu"]);
        }

        [Fact]
        public void BuildSortComparer_Given_ModifiedOnAscendingWithTiedTimestamps_Then_TiesOrderedByNameAscending()
        {
            // CreatedOn values are deliberately scrambled relative to the
            // expected order, so passing requires ModifiedOn to be the
            // primary key and Name the tie-breaker.
            List<IManagedNodeViewModel> nodes =
            [
                MakeNode(@"Charlie", s_LaterTime, s_SharedTime),
                MakeNode(@"Delta", s_EarlierTime, s_LaterTime),
                MakeNode(@"Alpha", s_LaterTime, s_SharedTime),
                MakeNode(@"Bravo", s_EarlierTime, s_SharedTime),
            ];

            nodes.Sort(ProjectScenarioNodeHelper.BuildSortComparer(SortMode.ModifiedOn, SortDirection.Ascending));

            nodes.Select(x => x.Name).ShouldBe([@"Alpha", @"Bravo", @"Charlie", @"Delta"]);
        }

        [Fact]
        public void BuildSortComparer_Given_NameAscendingWithDuplicateNames_Then_TiesOrderedByCreatedOnAscending()
        {
            NodeStub older = MakeNode(@"Alpha", s_EarlierTime);
            NodeStub newer = MakeNode(@"Alpha", s_LaterTime);
            NodeStub bravo = MakeNode(@"Bravo", s_SharedTime);
            List<IManagedNodeViewModel> nodes = [newer, bravo, older];

            nodes.Sort(ProjectScenarioNodeHelper.BuildSortComparer(SortMode.Name, SortDirection.Ascending));

            nodes.ShouldBe([older, newer, bravo]);
        }
    }
}
