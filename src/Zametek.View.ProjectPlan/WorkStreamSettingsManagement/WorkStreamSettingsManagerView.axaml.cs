using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class WorkStreamSettingsManagerView
        : UserControl
    {
        // One entry per column, in markup declaration order, so a live column's
        // header and value selector can be looked up by its position index when
        // building the clipboard table.
        private static readonly IReadOnlyList<(string Header, Func<IManagedWorkStreamViewModel, object?> Value)> s_CopyColumnDefinitions =
        [
            (Resource.ProjectPlan.Labels.Label_Id, static x => x.Id),
            (Resource.ProjectPlan.Labels.Label_IsPhase, static x => x.IsPhase),
            (Resource.ProjectPlan.Labels.Label_ColorFormat, static x => x.ColorFormat),
            (Resource.ProjectPlan.Labels.Label_WorkStreamName, static x => x.Name),
        ];

        public WorkStreamSettingsManagerView()
        {
            InitializeComponent();
        }

        public WorkStreamSettingsManagerView(
            IDataGridLayoutManager dataGridLayoutManager,
            IDataGridScrollManager dataGridScrollManager,
            ICommitEditHandler commitEditHandler)
        {
            ArgumentNullException.ThrowIfNull(dataGridLayoutManager);
            ArgumentNullException.ThrowIfNull(dataGridScrollManager);
            ArgumentNullException.ThrowIfNull(commitEditHandler);
            InitializeComponent();
            BehaviorCollection behaviors = Interaction.GetBehaviors(WorkStreamsGrid);
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
            if (DataContext is not IWorkStreamSettingsManagerViewModel vm)
            {
                return;
            }

            // Snapshot the rows in their underlying drag order.
            List<IManagedWorkStreamViewModel> rows = [.. vm.OrderableWorkStreams];

            await DataGridHelper.CopyTableToClipboardAsync(
                WorkStreamsGrid,
                s_CopyColumnDefinitions,
                rows,
                vm.ReportErrorAsync);
        }
    }
}
