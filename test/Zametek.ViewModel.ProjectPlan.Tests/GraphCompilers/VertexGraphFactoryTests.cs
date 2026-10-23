using Shouldly;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The factory of the application gives the builders and the compilers that a compile uses, and the schedule
    /// simulation takes it. A builder holds the state of one graph, so each call must give a new, empty object, and
    /// many threads must be able to use one factory.
    /// </summary>
    public class VertexGraphFactoryTests
    {
        #region Builders

        [Fact]
        public void CreateBuilder_Given_ACall_Then_GivesANewEmptyBuilderOfTheApplication()
        {
            var factory = new VertexGraphFactory();

            VertexGraphBuilder<int, int, int, IDependentActivity> builder = factory.CreateBuilder();

            builder.ShouldBeOfType<VertexGraphBuilder>();
            builder.ActivityIds.ShouldBeEmpty();
            builder.EventIds.ShouldBeEmpty();
        }

        [Fact]
        public void CreateBuilder_Given_TwoCalls_Then_TheBuildersAreNotTheSameAndShareNoGraph()
        {
            var factory = new VertexGraphFactory();
            VertexGraphBuilder<int, int, int, IDependentActivity> first = factory.CreateBuilder();
            VertexGraphBuilder<int, int, int, IDependentActivity> second = factory.CreateBuilder();

            first.AddActivity(new DependentActivity(1, 3, canBeRemoved: false), []).ShouldBeTrue();

            second.ShouldNotBeSameAs(first);
            first.ActivityIds.Count().ShouldBe(1);
            second.ActivityIds.ShouldBeEmpty();
        }

        #endregion

        #region Compilers

        [Fact]
        public void CreateCompiler_Given_TwoCalls_Then_GivesNewEmptyCompilersOfTheApplication()
        {
            var factory = new VertexGraphFactory();

            VertexGraphCompiler<int, int, int, IDependentActivity> first = factory.CreateCompiler();
            VertexGraphCompiler<int, int, int, IDependentActivity> second = factory.CreateCompiler();

            first.ShouldBeOfType<VertexGraphCompiler>();
            second.ShouldBeOfType<VertexGraphCompiler>();
            second.ShouldNotBeSameAs(first);
            ((VertexGraphCompiler)first).ActivityIds.ShouldBeEmpty();
            ((VertexGraphCompiler)second).ActivityIds.ShouldBeEmpty();
        }

        #endregion

        #region Many threads

        [Fact]
        public void Create_Given_ManyThreads_Then_EveryBuilderAndEveryCompilerIsNew()
        {
            const int c_Count = 64;
            var factory = new VertexGraphFactory();
            var builders = new ConcurrentBag<object>();
            var compilers = new ConcurrentBag<object>();

            Parallel.For(0, c_Count, _ =>
            {
                builders.Add(factory.CreateBuilder());
                compilers.Add(factory.CreateCompiler());
            });

            builders.Distinct(ReferenceEqualityComparer.Instance).Count().ShouldBe(c_Count);
            compilers.Distinct(ReferenceEqualityComparer.Instance).Count().ShouldBe(c_Count);
        }

        #endregion
    }
}
