using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class CommitEditHandler
        : ICommitEditHandler
    {
        private readonly HashSet<DataGrid> m_DataGrids;

        // The effort tracking view is absent here: its timesheet grids are
        // created from a data template (one per resource section), so they
        // cannot be registered statically.
        public CommitEditHandler(
            ActivitiesManagerView activitiesManagerView,
            ProgressTrackingManagerView progressTrackingManagerView,
            GraphSettingsManagerView graphSettingsManagerView,
            ResourceSettingsManagerView resourceSettingsManagerView,
            WorkStreamSettingsManagerView workStreamSettingsManagerView,
            HolidaySettingsManagerView holidaySettingsManagerView)
        {
            ArgumentNullException.ThrowIfNull(activitiesManagerView);
            ArgumentNullException.ThrowIfNull(progressTrackingManagerView);
            ArgumentNullException.ThrowIfNull(graphSettingsManagerView);
            ArgumentNullException.ThrowIfNull(resourceSettingsManagerView);
            ArgumentNullException.ThrowIfNull(workStreamSettingsManagerView);
            ArgumentNullException.ThrowIfNull(holidaySettingsManagerView);
            m_DataGrids = [];

            m_DataGrids.Add(activitiesManagerView.ActivitiesGrid);
            m_DataGrids.Add(progressTrackingManagerView.TrackerActivitiesGrid);
            m_DataGrids.Add(graphSettingsManagerView.ActivitySeveritiesGrid);
            m_DataGrids.Add(resourceSettingsManagerView.ResourcesGrid);
            m_DataGrids.Add(workStreamSettingsManagerView.WorkStreamsGrid);
            m_DataGrids.Add(holidaySettingsManagerView.HolidaysGrid);
        }

        // This is to handle the commitment of all datagrids when changing
        // project scenarios. It helps prevent thread locking if a datagrid
        // is still in edit mode while a new scenario is selected.
        public void CommitEdit()
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                foreach (DataGrid dataGrid in m_DataGrids)
                {
                    dataGrid.CommitEdit();
                }
            });
        }
    }
}
