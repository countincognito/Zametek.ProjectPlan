using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Zametek.Contract.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class ActivitiesManagerView
        : UserControl
    {
        // One entry per column, in markup declaration order, so a live column's
        // header and value selector can be looked up by its position index when
        // building the clipboard table. Selectors return raw values, except that
        // the time columns follow the grid's current Show Dates mode (day numbers
        // or dates), mirroring what the cells display.
        private static readonly IReadOnlyList<(string Header, Func<ManagedActivityViewModel, object?> Value)> s_CopyColumnDefinitions =
        [
            (Resource.ProjectPlan.Labels.Label_Id, static x => x.Id),
            (Resource.ProjectPlan.Labels.Label_ActivityName, static x => x.Name),
            (Resource.ProjectPlan.Labels.Label_Duration, static x => x.Duration),
            (Resource.ProjectPlan.Labels.Label_Dependencies, static x => x.DependenciesString),
            (Resource.ProjectPlan.Labels.Label_PlanningDependencies, static x => x.PlanningDependenciesString),
            (Resource.ProjectPlan.Labels.Label_ResourceDependencies, static x => x.ResourceDependenciesString),
            (Resource.ProjectPlan.Labels.Label_Successors, static x => x.SuccessorsString),
            (Resource.ProjectPlan.Labels.Label_IsDummy, static x => x.IsDummy),
            (Resource.ProjectPlan.Labels.Label_IsIsolated, static x => x.IsIsolated),
            (Resource.ProjectPlan.Labels.Label_IsCritical, static x => x.IsCritical),
            (Resource.ProjectPlan.Labels.Label_EarliestStartTime, static x => x.ShowDates ? x.EarliestStartDateTimeOffset : x.EarliestStartTime),
            (Resource.ProjectPlan.Labels.Label_LatestStartTime, static x => x.ShowDates ? x.LatestStartDateTimeOffset : x.LatestStartTime),
            (Resource.ProjectPlan.Labels.Label_EarliestFinishTime, static x => x.ShowDates ? x.EarliestFinishDateTimeOffset : x.EarliestFinishTime),
            (Resource.ProjectPlan.Labels.Label_LatestFinishTime, static x => x.ShowDates ? x.LatestFinishDateTimeOffset : x.LatestFinishTime),
            (Resource.ProjectPlan.Labels.Label_TotalSlack, static x => x.TotalSlack),
            (Resource.ProjectPlan.Labels.Label_FreeSlack, static x => x.FreeSlack),
            (Resource.ProjectPlan.Labels.Label_InterferingSlack, static x => x.InterferingSlack),
            (Resource.ProjectPlan.Labels.Label_MinimumFreeSlack, static x => x.MinimumFreeSlack),
            (Resource.ProjectPlan.Labels.Label_MinimumEarliestStartTime, static x => x.ShowDates ? x.MinimumEarliestStartDateTime : x.MinimumEarliestStartTime),
            (Resource.ProjectPlan.Labels.Label_MaximumLatestFinishTime, static x => x.ShowDates ? x.MaximumLatestFinishDateTime : x.MaximumLatestFinishTime),
            (Resource.ProjectPlan.Labels.Label_TargetWorkStreams, static x => x.WorkStreamSelector.TargetWorkStreamsString),
            (Resource.ProjectPlan.Labels.Label_TargetResources, static x => x.ResourceSelector.TargetResourcesString),
            (Resource.ProjectPlan.Labels.Label_HasNoCost, static x => x.HasNoCost),
            (Resource.ProjectPlan.Labels.Label_HasNoBilling, static x => x.HasNoBilling),
            (Resource.ProjectPlan.Labels.Label_HasNoEffort, static x => x.HasNoEffort),
            (Resource.ProjectPlan.Labels.Label_HasNoRisk, static x => x.HasNoRisk),
            (Resource.ProjectPlan.Labels.Label_OverrideColor, static x => x.OverrideColor),
            (Resource.ProjectPlan.Labels.Label_ColorFormat, static x => x.ColorFormat),
            (Resource.ProjectPlan.Labels.Label_TargetResourceOperator, static x => x.TargetResourceOperator),
            (Resource.ProjectPlan.Labels.Label_AllocatedToResources, static x => x.AllocatedToResourcesString),
            (Resource.ProjectPlan.Labels.Label_PercentageCompleted, static x => x.TrackerSet.LastTrackerValue),
            (Resource.ProjectPlan.Labels.Label_ActivityNotes, static x => x.Notes),
        ];

        public ActivitiesManagerView()
        {
            InitializeComponent();
        }

        public ActivitiesManagerView(
            IDataGridLayoutManager dataGridLayoutManager,
            IDataGridScrollManager dataGridScrollManager,
            ICommitEditHandler commitEditHandler)
        {
            ArgumentNullException.ThrowIfNull(dataGridLayoutManager);
            ArgumentNullException.ThrowIfNull(dataGridScrollManager);
            ArgumentNullException.ThrowIfNull(commitEditHandler);
            InitializeComponent();
            BehaviorCollection behaviors = Interaction.GetBehaviors(ActivitiesGrid);
            behaviors.Add(new DataGridPersistLayoutBehavior(dataGridLayoutManager));
            behaviors.Add(new DataGridPersistScrollBehavior(dataGridScrollManager));
            behaviors.Add(new DataGridCommitEditBehavior(commitEditHandler));
            behaviors.Add(new FadeInBehavior());
        }

        private async void CopyTable_Click(object? sender, RoutedEventArgs e)
        {
            await CopyTableToClipboardAsync();
        }

        private async Task CopyTableToClipboardAsync()
        {
            if (DataContext is not IActivitiesManagerViewModel vm)
            {
                return;
            }

            // Snapshot the rows in their underlying drag order. The selectors need
            // the concrete view-model type (some bound members are not on the
            // contract), which is also the type the cell templates bind against.
            List<ManagedActivityViewModel> rows = [.. vm.OrderableActivities.OfType<ManagedActivityViewModel>()];

            await DataGridHelper.CopyTableToClipboardAsync(
                ActivitiesGrid,
                s_CopyColumnDefinitions,
                rows,
                vm.ReportErrorAsync);
        }
    }
}
