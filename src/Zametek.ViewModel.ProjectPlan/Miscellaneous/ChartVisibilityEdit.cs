using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Whether an edit of the dialog of an activity, a resource or a work stream changes only the switch that hides it from
    /// the Gantt chart and the resource chart (IsHiddenFromCharts). Such an edit changes no input of a compile: it marks the
    /// scenario as changed and builds the two charts again, and it starts no compile and does not mark the outputs as stale.
    /// An edit that changes something more takes the path of an edit, which compiles, and the switch goes with it.
    /// </summary>
    /// <remarks>
    /// Each check names each mark of an edit of its model (Is...Edited, Is...Active). ChartVisibilityEditTests reads the
    /// marks of each model, and it fails when a mark that a check does not name is added.
    /// </remarks>
    public static class ChartVisibilityEdit
    {
        public static bool IsOnly(UpdateDependentActivityModel updateModel)
        {
            ArgumentNullException.ThrowIfNull(updateModel);
            return updateModel.IsIsHiddenFromChartsEdited
                && !updateModel.IsNameEdited
                && !updateModel.IsNotesEdited
                && !updateModel.IsTargetWorkStreamsEdited
                && !updateModel.IsTargetResourcesEdited
                && !updateModel.IsTargetResourceOperatorEdited
                && !updateModel.IsHasNoCostEdited
                && !updateModel.IsHasNoBillingEdited
                && !updateModel.IsHasNoEffortEdited
                && !updateModel.IsHasNoRiskEdited
                && !updateModel.IsOverrideColorEdited
                && !updateModel.IsColorFormatEdited;
        }

        public static bool IsOnly(UpdateResourceModel updateModel)
        {
            ArgumentNullException.ThrowIfNull(updateModel);
            return updateModel.IsIsHiddenFromChartsEdited
                && !updateModel.IsNameEdited
                && !updateModel.IsNotesEdited
                && !updateModel.IsIsExplicitTargetEdited
                && !updateModel.IsIsInactiveEdited
                && !updateModel.IsActivityAllocationTypeEdited
                && !updateModel.IsInterActivityAllocationTypeEdited
                && !updateModel.IsUnitCostEdited
                && !updateModel.IsUnitBillingEdited
                && !updateModel.IsFixedCostEdited
                && !updateModel.IsFixedBillingEdited
                && !updateModel.IsColorFormatActive
                && !updateModel.IsInterActivityPhasesEdited;
        }

        public static bool IsOnly(UpdateWorkStreamModel updateModel)
        {
            ArgumentNullException.ThrowIfNull(updateModel);
            return updateModel.IsIsHiddenFromChartsEdited
                && !updateModel.IsIsPhaseEdited
                && !updateModel.IsColorFormatActive;
        }
    }
}
