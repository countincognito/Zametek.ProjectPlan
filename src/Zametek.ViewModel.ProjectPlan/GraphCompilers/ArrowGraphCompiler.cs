using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ArrowGraphCompiler
        : ArrowGraphCompiler<int, int, int, IDependentActivity>
    {
        #region Ctors

        protected ArrowGraphCompiler(ArrowGraphBuilder<int, int, int, IDependentActivity> arrowGraphBuilder)
            : base(arrowGraphBuilder)
        {
        }

        public ArrowGraphCompiler()
            : this(ArrowGraphBuilder.CreateDependentActivityArrowGraphBuilder())
        {
        }

        #endregion
    }
}
