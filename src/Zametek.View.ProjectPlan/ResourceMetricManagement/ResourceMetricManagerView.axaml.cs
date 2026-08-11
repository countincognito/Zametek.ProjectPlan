using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
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

        // Copy the table as tab-separated text: a header row followed by one row per
        // resource, columns in their current display (drag) order with hidden columns
        // (e.g. the cost or billing groups) omitted - so the copy matches the visible
        // grid shape while carrying raw, invariant-culture values.
        private async Task CopyTableToClipboardAsync()
        {
            if (DataContext is not IResourceMetricManagerViewModel vm)
            {
                return;
            }

            try
            {
                List<(int DisplayIndex, string Header, Func<ResourceMetricsModel, object?> Value)> visibleColumns = [];

                int columnCount = Math.Min(ResourceMetricsGrid.Columns.Count, s_CopyColumnDefinitions.Count);

                for (int i = 0; i < columnCount; i++)
                {
                    DataGridColumn column = ResourceMetricsGrid.Columns[i];

                    if (!column.IsVisible)
                    {
                        continue;
                    }

                    (string header, Func<ResourceMetricsModel, object?> value) = s_CopyColumnDefinitions[i];
                    visibleColumns.Add((column.DisplayIndex, header, value));
                }

                visibleColumns.Sort(static (a, b) => a.DisplayIndex.CompareTo(b.DisplayIndex));

                List<string> lines =
                [
                    string.Join(DataGridHelper.Tab, visibleColumns.Select(static x => DataGridHelper.EscapeCellText(x.Header))),
                ];

                foreach (ResourceMetricsModel resourceMetrics in vm.ResourceMetrics)
                {
                    lines.Add(string.Join(DataGridHelper.Tab, visibleColumns.Select(x => DataGridHelper.FormatCellValue(x.Value(resourceMetrics)))));
                }

                string tableText = string.Join(Environment.NewLine, lines);

                IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard is null)
                {
                    return;
                }

                var item = new DataTransferItem();
                item.SetText(tableText);
                var dataTransfer = new DataTransfer();
                dataTransfer.Add(item);
                await clipboard.SetDataAsync(dataTransfer);
            }
            catch
            {
                // Best-effort: never crash if a clipboard backend cannot accept the text.
                await vm.ReportErrorAsync(Resource.ProjectPlan.Messages.Message_ClipboardCopyFailed);
            }
        }
    }
}
