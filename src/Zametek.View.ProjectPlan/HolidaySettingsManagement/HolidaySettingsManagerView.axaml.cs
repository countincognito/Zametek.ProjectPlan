using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class HolidaySettingsManagerView
        : UserControl
    {
        // One entry per column, in markup declaration order, so a live column's
        // header and value selector can be looked up by its position index when
        // building the clipboard table.
        private static readonly IReadOnlyList<(string Header, Func<IManagedHolidayViewModel, object?> Value)> s_CopyColumnDefinitions =
        [
            (Resource.ProjectPlan.Labels.Label_Id, static x => x.Id),
            (Resource.ProjectPlan.Labels.Label_HolidayName, static x => x.Name),
            (Resource.ProjectPlan.Labels.Label_HolidayStartDate, static x => x.StartDateTime),
            (Resource.ProjectPlan.Labels.Label_HolidayRecurrencePatternDisplay, static x => x.RecurrencePatternDisplay),
            (Resource.ProjectPlan.Labels.Label_HolidayNotes, static x => x.Notes),
        ];

        public HolidaySettingsManagerView()
        {
            InitializeComponent();
        }

        public HolidaySettingsManagerView(
            IDataGridLayoutManager dataGridLayoutManager,
            IDataGridScrollManager dataGridScrollManager)
        {
            ArgumentNullException.ThrowIfNull(dataGridLayoutManager);
            ArgumentNullException.ThrowIfNull(dataGridScrollManager);
            InitializeComponent();
            BehaviorCollection behaviors = Interaction.GetBehaviors(HolidaysGrid);
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
            if (DataContext is not IHolidaySettingsManagerViewModel vm)
            {
                return;
            }

            // Snapshot the rows in their current collection order.
            List<IManagedHolidayViewModel> rows = [.. vm.Holidays];

            await DataGridHelper.CopyTableToClipboardAsync(
                HolidaysGrid,
                s_CopyColumnDefinitions,
                rows,
                vm.ReportErrorAsync);
        }
    }
}
