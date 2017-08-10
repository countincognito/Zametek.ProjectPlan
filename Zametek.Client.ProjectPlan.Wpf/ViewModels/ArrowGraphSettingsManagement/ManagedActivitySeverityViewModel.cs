using Prism.Mvvm;
using System;
using Zametek.Common.Project;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class ManagedActivitySeverityViewModel
        : BindableBase
    {
        #region Fields

        private readonly ActivitySeverityDto m_ActivitySeverity;

        #endregion

        #region Ctors

        public ManagedActivitySeverityViewModel(ActivitySeverityDto activitySeverity)
        {
            m_ActivitySeverity = activitySeverity ?? throw new ArgumentNullException(nameof(activitySeverity));
        }

        #endregion

        #region Properties

        public ActivitySeverityDto ActivitySeverityDto => m_ActivitySeverity;

        public int SlackLimit
        {
            get
            {
                return m_ActivitySeverity.SlackLimit;
            }
            set
            {
                m_ActivitySeverity.SlackLimit = value;
                RaisePropertyChanged(nameof(SlackLimit));
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
                RaisePropertyChanged(nameof(CriticalityWeight));
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
                RaisePropertyChanged(nameof(FibonacciWeight));
            }
        }

        public ColorFormatDto ColorFormat
        {
            get
            {
                return m_ActivitySeverity.ColorFormat;
            }
            set
            {
                m_ActivitySeverity.ColorFormat = value;
                RaisePropertyChanged(nameof(ColorFormat));
            }
        }

        #endregion
    }
}
