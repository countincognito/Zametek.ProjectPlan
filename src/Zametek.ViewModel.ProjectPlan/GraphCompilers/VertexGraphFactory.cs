using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Makes the builders and the compilers of the application, for code that needs more than one of them. The
    /// schedule simulation takes one: it asks for a builder for each thread that runs iterations, and for a compiler
    /// for the first check of the plan.
    /// </summary>
    /// <remarks>
    /// Each call gives a new object, and the factory holds no state, so many threads can use one factory.
    /// </remarks>
    public sealed class VertexGraphFactory
        : IVertexGraphFactory<int, int, int, IDependentActivity>
    {
        public VertexGraphBuilder<int, int, int, IDependentActivity> CreateBuilder()
        {
            return VertexGraphBuilder.CreateDependentActivityVertexGraphBuilder();
        }

        public VertexGraphCompiler<int, int, int, IDependentActivity> CreateCompiler()
        {
            return new VertexGraphCompiler();
        }
    }
}
