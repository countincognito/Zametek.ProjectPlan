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
    public partial class SimulationMetricManagerView
        : UserControl
    {
        // One entry per column, in markup declaration order, so a live column's header and value selector can be looked
        // up by its position index when the clipboard table is built. The selectors return raw values (not the
        // display-formatted strings), so a pasted table is calculation-friendly. The path is the same list of activity
        // IDs as the grid shows.
        private static readonly IReadOnlyList<(string Header, Func<SimulationStatisticModel, object?> Value)> s_StatisticColumnDefinitions =
        [
            (Resource.ProjectPlan.Labels.Label_SimulationStatistic, static x => x.Name),
            (Resource.ProjectPlan.Labels.Label_SimulationValue, static x => x.Value),
        ];

        private static readonly IReadOnlyList<(string Header, Func<SimulationPathModel, object?> Value)> s_PathColumnDefinitions =
        [
            (Resource.ProjectPlan.Labels.Label_SimulationRank, static x => x.Rank),
            (Resource.ProjectPlan.Labels.Label_SimulationPath, static x => x.IdLabel),
            (Resource.ProjectPlan.Labels.Label_SimulationShare, static x => x.Share),
        ];

        private static readonly IReadOnlyList<(string Header, Func<SimulationActivityCriticalityModel, object?> Value)> s_CriticalityColumnDefinitions =
        [
            (Resource.ProjectPlan.Labels.Label_Id, static x => x.ActivityId),
            (Resource.ProjectPlan.Labels.Label_SimulationActivity, static x => x.Name),
            (Resource.ProjectPlan.Labels.Label_SimulationCriticality, static x => x.CriticalityIndex),
        ];

        public SimulationMetricManagerView()
        {
            InitializeComponent();
        }

        public SimulationMetricManagerView(
            IDataGridLayoutManager dataGridLayoutManager,
            IDataGridScrollManager dataGridScrollManager)
        {
            ArgumentNullException.ThrowIfNull(dataGridLayoutManager);
            ArgumentNullException.ThrowIfNull(dataGridScrollManager);
            InitializeComponent();

            foreach (DataGrid grid in new[] { SimulationStatisticsGrid, SimulationPathsGrid, SimulationCriticalitiesGrid })
            {
                BehaviorCollection behaviors = Interaction.GetBehaviors(grid);
                behaviors.Add(new DataGridPersistLayoutBehavior(dataGridLayoutManager));
                behaviors.Add(new DataGridPersistScrollBehavior(dataGridScrollManager));
            }

            Interaction.GetBehaviors(SimulationMetricsTabs).Add(new FadeInBehavior());
        }

        private async void CopyStatisticsTable_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not ISimulationMetricManagerViewModel vm)
            {
                return;
            }

            await DataGridHelper.CopyTableToClipboardAsync(
                SimulationStatisticsGrid,
                s_StatisticColumnDefinitions,
                vm.Simulation.Statistics,
                vm.ReportErrorAsync);
        }

        private async void CopyPathsTable_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not ISimulationMetricManagerViewModel vm)
            {
                return;
            }

            await DataGridHelper.CopyTableToClipboardAsync(
                SimulationPathsGrid,
                s_PathColumnDefinitions,
                vm.Simulation.Paths,
                vm.ReportErrorAsync);
        }

        private async void CopyCriticalitiesTable_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not ISimulationMetricManagerViewModel vm)
            {
                return;
            }

            await DataGridHelper.CopyTableToClipboardAsync(
                SimulationCriticalitiesGrid,
                s_CriticalityColumnDefinitions,
                vm.Simulation.Activities,
                vm.ReportErrorAsync);
        }
    }
}
