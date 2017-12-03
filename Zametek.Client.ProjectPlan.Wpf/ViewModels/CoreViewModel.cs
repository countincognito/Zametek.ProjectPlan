using Prism.Events;
using System;
using Zametek.Maths.Graphs;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class CoreViewModel
        : PropertyChangedPubSubViewModel, ICoreViewModel
    {
        #region Fields

        private readonly object m_Lock;
        private DateTime m_ProjectStart;
        private bool m_ShowDates;
        private bool m_UseBusinessDays;
        private bool m_HasStaleOutputs;
        private bool m_HasCompilationErrors;
        private GraphCompilation<int, IDependentActivity<int>> m_GraphCompilation;

        #endregion

        #region Ctors

        public CoreViewModel(IEventAggregator eventService)
            : base(eventService)
        {
            m_Lock = new object();
        }

        #endregion

        #region Properties

        public DateTime ProjectStart
        {
            get
            {
                return m_ProjectStart;
            }
            set
            {
                lock (m_Lock)
                {
                    m_ProjectStart = value;
                }
                RaisePropertyChanged();
            }
        }

        public bool ShowDates
        {
            get
            {
                return m_ShowDates;
            }
            set
            {
                lock (m_Lock)
                {
                    m_ShowDates = value;
                }
                RaisePropertyChanged();
            }
        }

        public bool UseBusinessDays
        {
            get
            {
                return m_UseBusinessDays;
            }
            set
            {
                lock (m_Lock)
                {
                    m_UseBusinessDays = value;
                }
                RaisePropertyChanged();
            }
        }

        public bool HasStaleOutputs
        {
            get
            {
                return m_HasStaleOutputs;
            }
            set
            {
                lock (m_Lock)
                {
                    m_HasStaleOutputs = value;
                }
                RaisePropertyChanged();
            }
        }

        public bool HasCompilationErrors
        {
            get
            {
                return m_HasCompilationErrors;
            }
            set
            {
                lock (m_Lock)
                {
                    m_HasCompilationErrors = value;
                }
                RaisePropertyChanged();
            }
        }

        public GraphCompilation<int, IDependentActivity<int>> GraphCompilation
        {
            get
            {
                return m_GraphCompilation;
            }
            set
            {
                lock (m_Lock)
                {
                    m_GraphCompilation = value;
                }
                RaisePropertyChanged();
            }
        }

        #endregion
    }
}
