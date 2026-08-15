using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class GraphSettingsManagerView
        : UserControl
    {
        // One entry per column, in markup declaration order, so a live column's
        // header and value selector can be looked up by its position index when
        // building the clipboard table. The unbounded severity band copies as
        // the display word ("Max") rather than the int.MaxValue sentinel, which
        // is an implementation detail, not data.
        private static readonly IReadOnlyList<(string Header, Func<IManagedActivitySeverityViewModel, object?> Value)> s_CopyColumnDefinitions =
        [
            (Resource.ProjectPlan.Labels.Label_SlackLimit, static x => x.SlackLimit == int.MaxValue ? Resource.ProjectPlan.Labels.Label_Max : (object?)x.SlackLimit),
            (Resource.ProjectPlan.Labels.Label_CriticalityWeight, static x => x.CriticalityWeight),
            (Resource.ProjectPlan.Labels.Label_FibonacciWeight, static x => x.FibonacciWeight),
            (Resource.ProjectPlan.Labels.Label_ColorFormat, static x => x.ColorFormat),
        ];

        public GraphSettingsManagerView()
        {
            InitializeComponent();
        }

        public GraphSettingsManagerView(
            IDataGridLayoutManager dataGridLayoutManager,
            IDataGridScrollManager dataGridScrollManager,
            ICommitEditHandler commitEditHandler)
        {
            ArgumentNullException.ThrowIfNull(dataGridLayoutManager);
            ArgumentNullException.ThrowIfNull(dataGridScrollManager);
            ArgumentNullException.ThrowIfNull(commitEditHandler);
            InitializeComponent();
            BehaviorCollection behaviors = Interaction.GetBehaviors(ActivitySeveritiesGrid);
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
            if (DataContext is not IGraphSettingsManagerViewModel vm)
            {
                return;
            }

            // Snapshot the rows in their current collection order.
            List<IManagedActivitySeverityViewModel> rows = [.. vm.ActivitySeverities];

            await DataGridHelper.CopyTableToClipboardAsync(
                ActivitySeveritiesGrid,
                s_CopyColumnDefinitions,
                rows,
                vm.ReportErrorAsync);
        }
    }
}
