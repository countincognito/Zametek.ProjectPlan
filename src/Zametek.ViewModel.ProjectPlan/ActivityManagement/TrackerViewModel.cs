using ReactiveUI;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class TrackerViewModel
        : ViewModelBase, ITrackerViewModel
    {
        #region Ctors

        public TrackerViewModel(
            int index,
            int time,
            int activityId,
            bool isIncluded = false,
            int percentageComplete = 0)
        {
            Index = index;
            Time = time;
            ActivityId = activityId;
            DisplayName = @$"{Resource.ProjectPlan.Labels.Label_Activity} {activityId} - {Resource.ProjectPlan.Labels.Label_Day} {time}";
            m_IsUpdated = false;
            m_IsIncluded = isIncluded;
            m_PercentageComplete = percentageComplete;
        }

        #endregion

        #region ITrackerViewModel Members

        public int Index { get; }

        public int Time { get; }

        public int ActivityId { get; }

        public string DisplayName { get; }

        private bool m_IsUpdated;
        public bool IsUpdated
        {
            get => m_IsUpdated;
            set => this.RaiseAndSetIfChanged(ref m_IsUpdated, value);
        }

        private bool m_IsIncluded;
        public bool IsIncluded
        {
            get => m_IsIncluded;
            set
            {
                if (m_IsIncluded != value)
                {
                    m_IsIncluded = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        private int m_PercentageComplete;
        public int PercentageComplete
        {
            get => m_PercentageComplete;
            set
            {
                if (m_PercentageComplete != value)
                {
                    m_PercentageComplete = value;
                    this.RaisePropertyChanged();
                    IsUpdated = true;
                }
            }
        }

        #endregion
    }
}
