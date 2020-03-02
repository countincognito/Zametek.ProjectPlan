using Prism.Mvvm;
using System;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class ManagedActivitySeverityViewModel
        : BindableBase
    {
        #region Fields

        private readonly Common.Project.v0_1_0.ActivitySeverityDto m_ActivitySeverity;

        #endregion

        #region Ctors

        public ManagedActivitySeverityViewModel(Common.Project.v0_1_0.ActivitySeverityDto activitySeverity)
        {
            m_ActivitySeverity = activitySeverity ?? throw new ArgumentNullException(nameof(activitySeverity));
        }

        #endregion

        #region Properties

        public Common.Project.v0_1_0.ActivitySeverityDto ActivitySeverityDto => m_ActivitySeverity;

        public int SlackLimit
        {
            get
            {
                return m_ActivitySeverity.SlackLimit;
            }
            set
            {
                m_ActivitySeverity.SlackLimit = value;
                RaisePropertyChanged();
            }
        }

        public double CriticalityWeight
        {
            get
            {
                return m_ActivitySeverity.CriticalityWeight;
            }
            set
            {
                m_ActivitySeverity.CriticalityWeight = value;
                RaisePropertyChanged();
            }
        }

        public double FibonacciWeight
        {
            get
            {
                return m_ActivitySeverity.FibonacciWeight;
            }
            set
            {
                m_ActivitySeverity.FibonacciWeight = value;
                RaisePropertyChanged();
            }
        }

        public Common.Project.v0_1_0.ColorFormatDto ColorFormat
        {
            get
            {
                return m_ActivitySeverity.ColorFormat;
            }
            set
            {
                m_ActivitySeverity.ColorFormat = value;
                RaisePropertyChanged();
            }
        }

        #endregion
    }
}
