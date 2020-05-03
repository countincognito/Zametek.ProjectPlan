using Prism.Mvvm;
using System;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ManagedActivitySeverityViewModel
        : BindableBase, IManagedActivitySeverityViewModel
    {
        #region Fields

        private readonly ActivitySeverityModel m_ActivitySeverity;

        #endregion

        #region Ctors

        public ManagedActivitySeverityViewModel(ActivitySeverityModel activitySeverity)
        {
            m_ActivitySeverity = activitySeverity ?? throw new ArgumentNullException(nameof(activitySeverity));
        }

        #endregion

        #region Properties

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

        public ColorFormatModel ColorFormat
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

        #region IManagedActivitySeverityViewModel Members

        public ActivitySeverityModel ActivitySeverity => m_ActivitySeverity;

        #endregion
    }
}
