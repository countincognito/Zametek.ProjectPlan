using Prism.Events;
using System;
using System.Collections.Generic;
using Zametek.Common.Project;
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
        private ArrowGraphSettingsDto m_ArrowGraphSettingsDto;
        private int? m_CyclomaticComplexity;
        private int? m_Duration;
        private double? m_DirectCost;
        private double? m_IndirectCost;
        private double? m_OtherCost;
        private double? m_TotalCost;

        #endregion

        #region Ctors

        public CoreViewModel(IEventAggregator eventService)
            : base(eventService)
        {
            m_Lock = new object();
            ResourceDtos = new List<ResourceDto>();
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

        public IList<ResourceDto> ResourceDtos
        {
            get;
        }

        public ArrowGraphSettingsDto ArrowGraphSettingsDto
        {
            get
            {
                return m_ArrowGraphSettingsDto;
            }
            set
            {
                lock (m_Lock)
                {
                    m_ArrowGraphSettingsDto = value;
                }
                RaisePropertyChanged();
            }
        }

        public int? CyclomaticComplexity
        {
            get
            {
                return m_CyclomaticComplexity;
            }
            set
            {
                m_CyclomaticComplexity = value;
                RaisePropertyChanged();
            }
        }

        public int? Duration
        {
            get
            {
                return m_Duration;
            }
            set
            {
                m_Duration = value;
                RaisePropertyChanged();
            }
        }

        public double? DirectCost
        {
            get
            {
                return m_DirectCost;
            }
            set
            {
                m_DirectCost = value;
                RaisePropertyChanged();
            }
        }

        public double? IndirectCost
        {
            get
            {
                return m_IndirectCost;
            }
            set
            {
                m_IndirectCost = value;
                RaisePropertyChanged();
            }
        }

        public double? OtherCost
        {
            get
            {
                return m_OtherCost;
            }
            set
            {
                m_OtherCost = value;
                RaisePropertyChanged();
            }
        }

        public double? TotalCost
        {
            get
            {
                return m_TotalCost;
            }
            set
            {
                m_TotalCost = value;
                RaisePropertyChanged();
            }
        }

        #endregion
    }
}
