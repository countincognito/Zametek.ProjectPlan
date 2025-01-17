using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface IResourceEditViewModel
        : IDisposable
    {
        bool IsExplicitTarget { get; set; }
        bool IsIsExplicitTargetActive { get; set; }

        bool IsInactive { get; set; }
        bool IsIsInactiveActive { get; set; }

        InterActivityAllocationType InterActivityAllocationType { get; set; }
        bool IsInterActivityAllocationTypeActive { get; set; }

        double UnitCost { get; set; }
        bool IsUnitCostActive { get; set; }

        IWorkStreamSelectorViewModel WorkStreamSelector { get; }
        bool IsWorkStreamSelectorActive { get; set; }

        UpdateDependentResourceModel BuildUpdateModel();
    }
}
