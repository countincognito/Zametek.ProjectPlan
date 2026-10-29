using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for the compiler of the application, in the part that the live graph of a plan needs: setting the dependencies
    /// that the user entered without leaving the links of the resources in the graph.
    /// </summary>
    /// <remarks>
    /// A resource dependency is a result of a compilation, and a compilation writes it into the activity. The library makes
    /// the links of an activity from its dependencies, its planning dependencies and the resource dependencies that the
    /// activity holds. So a test gives the activity a resource dependency, as a compilation does, and reads the links of the
    /// graph from the builder that the compiler was made over.
    /// </remarks>
    public class VertexGraphCompilerTests
    {
        [Fact]
        public void SetUserDependencies_Given_AResourceDependencyOfTheActivity_Then_TheGraphHoldsNoLinkOfTheResource()
        {
            ProbeCompiler compiler = CreateCompiler(out _, out DependentActivity second);
            second.ResourceDependencies.Add(1);

            bool isSet = compiler.SetUserDependencies(2, [], []);

            isSet.ShouldBeTrue();
            compiler.LinksOf(2).ShouldBeEmpty();
            compiler.IsIsolated(1).ShouldBeTrue();
            compiler.IsIsolated(2).ShouldBeTrue();
            second.ResourceDependencies.ShouldBe(new HashSet<int> { 1 }, "a compilation gave it, and an edit leaves it");
            second.Dependencies.ShouldBeEmpty();
            second.PlanningDependencies.ShouldBeEmpty();
        }

        [Fact]
        public void SetActivityDependencies_Given_AResourceDependencyOfTheActivity_Then_TheGraphHoldsTheLinkOfTheResource()
        {
            // This is the behavior of the library that SetUserDependencies exists for. If a version of the library stops doing
            // this, SetUserDependencies has nothing to take out, and it can go.
            ProbeCompiler compiler = CreateCompiler(out _, out DependentActivity second);
            second.ResourceDependencies.Add(1);

            compiler.SetActivityDependencies(2, [], []);

            compiler.LinksOf(2).ShouldBe(new[] { 1 });
            compiler.IsIsolated(1).ShouldBeFalse();
            compiler.IsIsolated(2).ShouldBeFalse();
        }

        [Fact]
        public void SetUserDependencies_Given_ADependencyOfTheUser_Then_TheLinkAndTheDependencyAreThere()
        {
            ProbeCompiler compiler = CreateCompiler(out _, out DependentActivity second);
            var third = new DependentActivity(3, 2, canBeRemoved: false);
            compiler.AddActivity(third);
            compiler.SetUserDependencies(3, [1], []);
            second.ResourceDependencies.Add(1);

            bool isSet = compiler.SetUserDependencies(2, [1], []);

            isSet.ShouldBeTrue();
            second.Dependencies.ShouldBe(new HashSet<int> { 1 });
            compiler.LinksOf(2).ShouldBe(new[] { 1 });
            compiler.LinksOf(3).ShouldBe(new[] { 1 }, "the link of the other activity is not touched");
            compiler.IsIsolated(1).ShouldBeFalse();
            compiler.IsIsolated(2).ShouldBeFalse();
        }

        [Fact]
        public void SetUserDependencies_Given_APlanningDependencyOfTheUser_Then_TheLinkAndThePlanningDependencyAreThere()
        {
            ProbeCompiler compiler = CreateCompiler(out _, out DependentActivity second);
            second.ResourceDependencies.Add(1);

            bool isSet = compiler.SetUserDependencies(2, [], [1]);

            isSet.ShouldBeTrue();
            second.Dependencies.ShouldBeEmpty();
            second.PlanningDependencies.ShouldBe(new HashSet<int> { 1 });
            compiler.LinksOf(2).ShouldBe(new[] { 1 });
        }

        [Fact]
        public void SetUserDependencies_Given_ADependencyThatAResourceGaveToo_Then_TheLinkStaysUntilTheUserTakesTheDependencyAway()
        {
            ProbeCompiler compiler = CreateCompiler(out _, out DependentActivity second);
            second.ResourceDependencies.Add(1);

            compiler.SetUserDependencies(2, [1], []);

            second.Dependencies.ShouldBe(new HashSet<int> { 1 });
            compiler.LinksOf(2).ShouldBe(new[] { 1 });

            compiler.SetUserDependencies(2, [], []);

            second.Dependencies.ShouldBeEmpty();
            compiler.LinksOf(2).ShouldBeEmpty("the resource still makes the second wait, but that is not a link of the user");
            second.ResourceDependencies.ShouldBe(new HashSet<int> { 1 });
        }

        [Fact]
        public void SetUserDependencies_Given_ALinkThatAnEarlierCallLeft_Then_TakesItOut()
        {
            ProbeCompiler compiler = CreateCompiler(out _, out DependentActivity second);
            second.ResourceDependencies.Add(1);
            compiler.SetActivityDependencies(2, [], []);
            compiler.LinksOf(2).ShouldBe(new[] { 1 }, "the test cannot tell a link of a resource from none");

            compiler.SetUserDependencies(2, [], []);

            compiler.LinksOf(2).ShouldBeEmpty();
            compiler.IsIsolated(1).ShouldBeTrue();
            compiler.IsIsolated(2).ShouldBeTrue();
        }

        [Fact]
        public void SetUserDependencies_Given_ALinkOfAResourceThatNoLongerHoldsForTheActivity_Then_TakesItOut()
        {
            // The compilation that came after the link was made gave the activity other resource dependencies.
            ProbeCompiler compiler = CreateCompiler(out _, out DependentActivity second);
            second.ResourceDependencies.Add(1);
            compiler.SetActivityDependencies(2, [], []);
            second.ResourceDependencies.Clear();

            compiler.SetUserDependencies(2, [], []);

            compiler.LinksOf(2).ShouldBeEmpty();
            compiler.IsIsolated(1).ShouldBeTrue();
        }

        [Fact]
        public void SetUserDependencies_Given_AnActivityThatTheGraphDoesNotHold_Then_ReturnsFalseAndChangesNothing()
        {
            ProbeCompiler compiler = CreateCompiler(out _, out DependentActivity second);
            second.ResourceDependencies.Add(1);

            bool isSet = compiler.SetUserDependencies(99, [1], []);

            isSet.ShouldBeFalse();
            compiler.LinksOf(2).ShouldBeEmpty();
            compiler.IsIsolated(1).ShouldBeTrue();
            compiler.IsIsolated(2).ShouldBeTrue();
        }

        // A compiler over a builder that the test keeps, so that the test can read the links of the graph.
        private sealed class ProbeCompiler
            : VertexGraphCompiler
        {
            public ProbeCompiler()
                : this(VertexGraphBuilder.CreateDependentActivityVertexGraphBuilder())
            {
            }

            private ProbeCompiler(VertexGraphBuilder builder)
                : base(builder)
            {
                Builder = builder;
            }

            public VertexGraphBuilder Builder { get; }

            /// <summary>
            /// The ids of the activities that the graph links the activity to as its dependencies, in order.
            /// </summary>
            public int[] LinksOf(int activityId) => [.. Builder.ActivityDependencyIds(activityId).OrderBy(x => x)];
        }

        // Two activities that depend on nothing.
        private static ProbeCompiler CreateCompiler(out DependentActivity first, out DependentActivity second)
        {
            var compiler = new ProbeCompiler();
            first = new DependentActivity(1, 2, canBeRemoved: false);
            second = new DependentActivity(2, 2, canBeRemoved: false);
            compiler.AddActivity(first).ShouldBeTrue();
            compiler.AddActivity(second).ShouldBeTrue();
            return compiler;
        }
    }
}
