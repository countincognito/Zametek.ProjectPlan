using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class ResourceMetricManagerView
        : UserControl
    {
        // One entry per column, in markup declaration order, so a live column's
        // header and value selector can be looked up by its position index when
        // building the clipboard table. Selectors return raw values (not the
        // display-formatted strings), so a pasted table is calculation-friendly.
        private static readonly IReadOnlyList<(string Header, Func<ResourceMetricsModel, object?> Value)> s_CopyColumnDefinitions =
        [
            (Resource.ProjectPlan.Labels.Label_Id, static x => x.ResourceId),
            (Resource.ProjectPlan.Labels.Label_Name, static x => x.ResourceName),
            (Resource.ProjectPlan.Labels.Label_ResourceMetricDirectCost, static x => x.Costs.Direct),
            (Resource.ProjectPlan.Labels.Label_ResourceMetricIndirectCost, static x => x.Costs.Indirect),
            (Resource.ProjectPlan.Labels.Label_ResourceMetricOtherCost, static x => x.Costs.Other),
            (Resource.ProjectPlan.Labels.Label_ResourceMetricTotalCost, static x => x.Costs.Total),
            (Resource.ProjectPlan.Labels.Label_ResourceMetricDirectBilling, static x => x.Billings.Direct),
            (Resource.ProjectPlan.Labels.Label_ResourceMetricIndirectBilling, static x => x.Billings.Indirect),
            (Resource.ProjectPlan.Labels.Label_ResourceMetricOtherBilling, static x => x.Billings.Other),
            (Resource.ProjectPlan.Labels.Label_ResourceMetricTotalBilling, static x => x.Billings.Total),
            (Resource.ProjectPlan.Labels.Label_ResourceMetricDirectEffort, static x => x.Efforts.Direct),
            (Resource.ProjectPlan.Labels.Label_ResourceMetricIndirectEffort, static x => x.Efforts.Indirect),
            (Resource.ProjectPlan.Labels.Label_ResourceMetricOtherEffort, static x => x.Efforts.Other),
            (Resource.ProjectPlan.Labels.Label_ResourceMetricTotalEffort, static x => x.Efforts.Total),
            (Resource.ProjectPlan.Labels.Label_ResourceMetricActivityEffort, static x => x.Efforts.Activity),
            (Resource.ProjectPlan.Labels.Label_ResourceMetricEffortEfficiency, static x => x.Efforts.Efficiency),
        ];

        public ResourceMetricManagerView()
        {
            InitializeComponent();
        }

        public ResourceMetricManagerView(
            IDataGridLayoutManager dataGridLayoutManager,
            IDataGridScrollManager dataGridScrollManager)
        {
            ArgumentNullException.ThrowIfNull(dataGridLayoutManager);
            ArgumentNullException.ThrowIfNull(dataGridScrollManager);
            InitializeComponent();
            BehaviorCollection behaviors = Interaction.GetBehaviors(ResourceMetricsGrid);
            behaviors.Add(new DataGridPersistLayoutBehavior(dataGridLayoutManager));
            behaviors.Add(new DataGridPersistScrollBehavior(dataGridScrollManager));
            behaviors.Add(new FadeInBehavior());
        }

        private async void CopyTable_Click(object? sender, RoutedEventArgs e)
        {
            await CopyTableToClipboardAsync();
        }

        private async Task CopyTableToClipboardAsync()
        {
            if (DataContext is not IResourceMetricManagerViewModel vm)
            {
                return;
            }

            await DataGridHelper.CopyTableToClipboardAsync(
                ResourceMetricsGrid,
                s_CopyColumnDefinitions,
                vm.ResourceMetrics,
                vm.ReportErrorAsync);
        }
    }
}
