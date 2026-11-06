using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The builder of the application counts the IDs of its events down from -1. It asks an ID generator factory
    /// for its generator, so a builder that loads a graph, and the clone of a builder, start under the lowest ID that
    /// the graph holds.
    /// </summary>
    public class VertexGraphBuilderTests
    {
        // A chain of activities: 1, then 2 after 1, then 3 after 2, and so on. A chain of n activities has n - 1 events.
        private static void AddChain(VertexGraphBuilder builder, int count)
        {
            for (int id = 1; id <= count; id++)
            {
                HashSet<int> dependencies = id == 1 ? [] : [id - 1];
                builder.AddActivity(new DependentActivity(id, id + 2, canBeRemoved: false), dependencies).ShouldBeTrue();
            }
        }

        [Fact]
        public void CreateDependentActivityVertexGraphBuilder_Given_ACall_Then_GivesANewEmptyBuilder()
        {
            VertexGraphBuilder first = VertexGraphBuilder.CreateDependentActivityVertexGraphBuilder();
            VertexGraphBuilder second = VertexGraphBuilder.CreateDependentActivityVertexGraphBuilder();

            second.ShouldNotBeSameAs(first);
            first.ActivityIds.ShouldBeEmpty();
            first.EventIds.ShouldBeEmpty();
        }

        [Fact]
        public void CreateDependentActivityVertexGraphBuilder_Given_AChain_Then_TheEventsCountDownFromMinusOne()
        {
            VertexGraphBuilder builder = VertexGraphBuilder.CreateDependentActivityVertexGraphBuilder();

            AddChain(builder, 3);

            builder.EventIds.OrderByDescending(x => x).ShouldBe([-1, -2]);
        }

        // The clone of a builder of the application is a builder of the application. Its events follow the events of its
        // original, with no number left out: the next event of the clone and the next event of the original have the same ID.
        [Fact]
        public void CloneObject_Given_AChain_Then_TheCloneIsABuilderOfTheApplicationAndCountsOnWithoutAGap()
        {
            VertexGraphBuilder original = VertexGraphBuilder.CreateDependentActivityVertexGraphBuilder();
            AddChain(original, 3);

            object clone = original.CloneObject();

            var cloneBuilder = clone.ShouldBeOfType<VertexGraphBuilder>();
            cloneBuilder.AddActivity(new DependentActivity(4, 2, canBeRemoved: false), [3]).ShouldBeTrue();
            original.AddActivity(new DependentActivity(4, 2, canBeRemoved: false), [3]).ShouldBeTrue();

            cloneBuilder.EventIds.OrderByDescending(x => x).ShouldBe([-1, -2, -3]);
            original.EventIds.OrderByDescending(x => x).ShouldBe([-1, -2, -3]);
        }

        [Fact]
        public void CloneObject_Given_AnEmptyBuilder_Then_TheCloneCountsFromMinusOne()
        {
            VertexGraphBuilder original = VertexGraphBuilder.CreateDependentActivityVertexGraphBuilder();

            var clone = original.CloneObject().ShouldBeOfType<VertexGraphBuilder>();
            AddChain(clone, 3);

            clone.EventIds.OrderByDescending(x => x).ShouldBe([-1, -2]);
        }
    }
}
