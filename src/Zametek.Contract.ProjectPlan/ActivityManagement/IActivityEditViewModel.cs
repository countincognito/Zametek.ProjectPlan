using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface IActivityEditViewModel
        : IDisposable
    {
        IResourceSelectorViewModel ResourceSelector { get; }
        bool IsResourceSelectorActive { get; set; }

        IWorkStreamSelectorViewModel WorkStreamSelector { get; }
        bool IsWorkStreamSelectorActive { get; set; }

        LogicalOperator TargetResourceOperator { get; set; }
        bool IsTargetResourceOperatorActive { get; set; }

        UpdateActivityModel BuildUpdateActivityModel();
    }
}
