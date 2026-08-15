using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class ResourceSettingsManagerView
        : UserControl
    {
        // One entry per column, in markup declaration order, so a live column's
        // header and value selector can be looked up by its position index when
        // building the clipboard table. Selectors return raw values (enums copy
        // by name, colors as HTML hex codes).
        private static readonly IReadOnlyList<(string Header, Func<IManagedResourceViewModel, object?> Value)> s_CopyColumnDefinitions =
        [
            (Resource.ProjectPlan.Labels.Label_Id, static x => x.Id),
            (Resource.ProjectPlan.Labels.Label_ResourceName, static x => x.Name),
            (Resource.ProjectPlan.Labels.Label_IsExplicitTarget, static x => x.IsExplicitTarget),
            (Resource.ProjectPlan.Labels.Label_IsInactive, static x => x.IsInactive),
            (Resource.ProjectPlan.Labels.Label_AllocationOrder, static x => x.AllocationOrder),
            (Resource.ProjectPlan.Labels.Label_ActivityAllocationType, static x => x.ActivityAllocationType),
            (Resource.ProjectPlan.Labels.Label_InterActivityAllocationType, static x => x.InterActivityAllocationType),
            (Resource.ProjectPlan.Labels.Label_InterActivityPhases, static x => x.WorkStreamSelector.TargetWorkStreamsString),
            (Resource.ProjectPlan.Labels.Label_UnitCost, static x => x.UnitCost),
            (Resource.ProjectPlan.Labels.Label_UnitBilling, static x => x.UnitBilling),
            (Resource.ProjectPlan.Labels.Label_FixedCost, static x => x.FixedCost),
            (Resource.ProjectPlan.Labels.Label_FixedBilling, static x => x.FixedBilling),
            (Resource.ProjectPlan.Labels.Label_ColorFormat, static x => x.ColorFormat),
            (Resource.ProjectPlan.Labels.Label_ResourceNotes, static x => x.Notes),
        ];

        public ResourceSettingsManagerView()
        {
            InitializeComponent();
        }

        public ResourceSettingsManagerView(
            IDataGridLayoutManager dataGridLayoutManager,
            IDataGridScrollManager dataGridScrollManager,
            ICommitEditHandler commitEditHandler)
        {
            ArgumentNullException.ThrowIfNull(dataGridLayoutManager);
            ArgumentNullException.ThrowIfNull(dataGridScrollManager);
            ArgumentNullException.ThrowIfNull(commitEditHandler);
            InitializeComponent();
            BehaviorCollection behaviors = Interaction.GetBehaviors(ResourcesGrid);
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
            if (DataContext is not IResourceSettingsManagerViewModel vm)
            {
                return;
            }

            // Snapshot the rows in their underlying drag order.
            List<IManagedResourceViewModel> rows = [.. vm.OrderableResources];

            await DataGridHelper.CopyTableToClipboardAsync(
                ResourcesGrid,
                s_CopyColumnDefinitions,
                rows,
                vm.ReportErrorAsync);
        }
    }
}
