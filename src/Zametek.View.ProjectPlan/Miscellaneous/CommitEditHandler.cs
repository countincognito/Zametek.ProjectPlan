using Avalonia.Threading;
using System;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class CommitEditHandler
        : ICommitEditHandler
    {
        private readonly ActivitiesManagerView m_ActivitiesManagerView;
        private readonly TrackingManagerView m_TrackingManagerView;
        private readonly GraphSettingsManagerView m_GraphSettingsManagerView;
        private readonly ResourceSettingsManagerView m_ResourceSettingsManagerView;
        private readonly WorkStreamSettingsManagerView m_WorkStreamSettingsManagerView;
        private readonly HolidaySettingsManagerView m_HolidaySettingsManagerView;

        // This is to handle the commitment of all datagrids when changing
        // project scenarios. It helps prevent thread locking if a datagrid
        // is still in edit mode while a new scenario is selected.
        public CommitEditHandler(
            ActivitiesManagerView activitiesManagerView,
            TrackingManagerView trackingManagerView,
            GraphSettingsManagerView graphSettingsManagerView,
            ResourceSettingsManagerView resourceSettingsManagerView,
            WorkStreamSettingsManagerView workStreamSettingsManagerView,
            HolidaySettingsManagerView holidaySettingsManagerView)
        {
            m_ActivitiesManagerView = activitiesManagerView ?? throw new ArgumentNullException(nameof(activitiesManagerView));
            m_TrackingManagerView = trackingManagerView ?? throw new ArgumentNullException(nameof(trackingManagerView));
            m_GraphSettingsManagerView = graphSettingsManagerView ?? throw new ArgumentNullException(nameof(graphSettingsManagerView));
            m_ResourceSettingsManagerView = resourceSettingsManagerView ?? throw new ArgumentNullException(nameof(resourceSettingsManagerView));
            m_WorkStreamSettingsManagerView = workStreamSettingsManagerView ?? throw new ArgumentNullException(nameof(workStreamSettingsManagerView));
            m_HolidaySettingsManagerView = holidaySettingsManagerView ?? throw new ArgumentNullException(nameof(holidaySettingsManagerView));
        }

        public void CommitEdit()
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                m_ActivitiesManagerView.ActivitiesGrid.CommitEdit();
                m_TrackingManagerView.TrackerResourcesGrid.CommitEdit();
                m_TrackingManagerView.TrackerActivitiesGrid.CommitEdit();
                m_GraphSettingsManagerView.ActivitySeveritiesGrid.CommitEdit();
                m_ResourceSettingsManagerView.ResourcesGrid.CommitEdit();
                m_WorkStreamSettingsManagerView.WorkStreamsGrid.CommitEdit();
                m_HolidaySettingsManagerView.HolidaysGrid.CommitEdit();
            });
        }
    }
}
