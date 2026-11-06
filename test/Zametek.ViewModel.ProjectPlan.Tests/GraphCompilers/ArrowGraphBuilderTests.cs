using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The builder of the application counts the IDs of its nodes and of its dummy activities down from -1. It asks
    /// an ID generator factory for each of its two generators, so a builder that loads a graph, and the clone of a
    /// builder, start under the lowest ID that the graph holds. The graph that a clone loads is the copy that ToGraph
    /// gives, and ToGraph first removes the dummy activities that the graph does not need, so these tests take the IDs
    /// that the clone holds when it starts, and not the IDs that its original held.
    /// </summary>
    public class ArrowGraphBuilderTests
    {
        private static void Add(ArrowGraphBuilder builder, int id, params int[] dependencies)
        {
            builder.AddActivity(new DependentActivity(id, id + 1, canBeRemoved: false), [.. dependencies]).ShouldBeTrue();
        }

        // 3 needs 1 and 2, and 4 needs only 2, which needs a dummy activity.
        private static void AddActivitiesThatNeedADummy(ArrowGraphBuilder builder)
        {
            Add(builder, 3, 1, 2);
            Add(builder, 4, 2);
        }

        // A new builder holds no activity. It holds a start node and an end node, and their IDs are the first that the
        // generator of the nodes gives.
        [Fact]
        public void CreateDependentActivityArrowGraphBuilder_Given_ACall_Then_GivesANewBuilderWithOnlyAStartNodeAndAnEndNode()
        {
            ArrowGraphBuilder first = ArrowGraphBuilder.CreateDependentActivityArrowGraphBuilder();
            ArrowGraphBuilder second = ArrowGraphBuilder.CreateDependentActivityArrowGraphBuilder();

            second.ShouldNotBeSameAs(first);
            first.ActivityIds.ShouldBeEmpty();
            first.EdgeIds.ShouldBeEmpty();
            first.NodeIds.OrderByDescending(x => x).ShouldBe([-1, -2]);
        }

        [Fact]
        public void CreateDependentActivityArrowGraphBuilder_Given_ActivitiesThatNeedADummy_Then_TheDummiesAndTheNodesCountDown()
        {
            ArrowGraphBuilder builder = ArrowGraphBuilder.CreateDependentActivityArrowGraphBuilder();
            Add(builder, 1);
            Add(builder, 2);

            AddActivitiesThatNeedADummy(builder);

            builder.NodeIds.ShouldAllBe(x => x < 0);
            List<int> dummies = builder.EdgeIds.Except([1, 2, 3, 4]).ToList();
            dummies.ShouldNotBeEmpty();
            dummies.ShouldAllBe(x => x < 0);
        }

        // The clone is a builder of the application. What it makes has IDs under the lowest IDs that its graph holds when
        // it starts, and so no ID is used twice.
        [Fact]
        public void CloneObject_Given_ADummyActivityBeforeTheClone_Then_TheCloneIsABuilderOfTheApplicationAndMakesIdsUnderTheLowestIdsOfItsGraph()
        {
            ArrowGraphBuilder original = ArrowGraphBuilder.CreateDependentActivityArrowGraphBuilder();
            Add(original, 1);
            Add(original, 2);
            AddActivitiesThatNeedADummy(original);

            var clone = original.CloneObject().ShouldBeOfType<ArrowGraphBuilder>();
            List<int> edgesAtStart = clone.EdgeIds.ToList();
            List<int> nodesAtStart = clone.NodeIds.ToList();
            edgesAtStart.Except([1, 2, 3, 4]).ShouldNotBeEmpty();

            Add(clone, 5, 3, 4);

            List<int> nodes = clone.NodeIds.Except(nodesAtStart).ToList();
            nodes.ShouldNotBeEmpty();
            nodes.ShouldAllBe(x => x < nodesAtStart.Min());
            clone.EdgeIds.Except(edgesAtStart).Except([5]).ShouldAllBe(x => x < edgesAtStart.Min());
            clone.NodeIds.Distinct().Count().ShouldBe(clone.NodeIds.Count());
            clone.EdgeIds.Distinct().Count().ShouldBe(clone.EdgeIds.Count());
        }

        // The copy that the clone loads holds the activity 1 and no dummy activity, and so the lowest ID of its edges is
        // positive. The dummy activities that the clone makes afterwards count down from -1, and not from the ID under 1.
        [Fact]
        public void CloneObject_Given_OneActivityBeforeTheClone_Then_TheDummiesOfTheCloneCountDownFromMinusOne()
        {
            ArrowGraphBuilder original = ArrowGraphBuilder.CreateDependentActivityArrowGraphBuilder();
            Add(original, 1);

            var clone = original.CloneObject().ShouldBeOfType<ArrowGraphBuilder>();
            clone.EdgeIds.ShouldBe([1]);

            Add(clone, 2);
            AddActivitiesThatNeedADummy(clone);

            List<int> dummies = clone.EdgeIds.Except([1, 2, 3, 4]).ToList();
            dummies.ShouldNotBeEmpty();
            dummies.ShouldAllBe(x => x < 0);
            dummies.ShouldContain(-1);
        }
    }
}
