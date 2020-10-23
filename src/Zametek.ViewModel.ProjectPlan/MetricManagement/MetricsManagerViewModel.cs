using AutoMapper;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Event.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class MetricsManagerViewModel
        : PropertyChangedPubSubViewModel, IMetricsManagerViewModel
    {
        #region Fields

        private readonly object m_Lock;

        private double? m_CriticalityRisk;
        private double? m_FibonacciRisk;
        private double? m_ActivityRisk;
        private double? m_ActivityRiskWithStdDevCorrection;
        private double? m_GeometricCriticalityRisk;
        private double? m_GeometricFibonacciRisk;
        private double? m_GeometricActivityRisk;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IProjectService m_ProjectService;
        private readonly IMapper m_Mapper;
        private readonly IEventAggregator m_EventService;

        private SubscriptionToken m_GraphCompilationUpdatedSubscriptionToken;

        #endregion

        #region Ctors

        public MetricsManagerViewModel(
            ICoreViewModel coreViewModel,
            IProjectService projectService,
            IMapper mapper,
            IEventAggregator eventService)
            : base(eventService)
        {
            m_Lock = new object();
            m_CoreViewModel = coreViewModel ?? throw new ArgumentNullException(nameof(coreViewModel));
            m_ProjectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            m_Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));

            SubscribeToEvents();

            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.IsBusy), nameof(IsBusy), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.HasCompilationErrors), nameof(HasCompilationErrors), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.HasStaleOutputs), nameof(HasStaleOutputs), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.CyclomaticComplexity), nameof(CyclomaticComplexity), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.DurationManMonths), nameof(DurationManMonths), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.DirectCost), nameof(DirectCost), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.IndirectCost), nameof(IndirectCost), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.OtherCost), nameof(OtherCost), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.TotalCost), nameof(TotalCost), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.Efficiency), nameof(Efficiency), ThreadOption.BackgroundThread);
        }

        #endregion

        #region Properties

        private IGraphCompilation<int, int, IDependentActivity<int, int>> GraphCompilation => m_CoreViewModel.GraphCompilation;

        private ArrowGraphSettingsModel ArrowGraphSettings => m_CoreViewModel.ArrowGraphSettings;

        private MetricsModel Metrics
        {
            get
            {
                return m_CoreViewModel.Metrics;
            }
            set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.Metrics = value;
                }
            }
        }

        #endregion

        #region Private Methods

        private void SubscribeToEvents()
        {
            m_GraphCompilationUpdatedSubscriptionToken =
                m_EventService.GetEvent<PubSubEvent<GraphCompilationUpdatedPayload>>()
                    .Subscribe(payload =>
                    {
                        IsBusy = true;
                        CalculateRiskMetrics();
                        IsBusy = false;
                    }, ThreadOption.BackgroundThread);
        }

        private void UnsubscribeFromEvents()
        {
            m_EventService.GetEvent<PubSubEvent<GraphCompilationUpdatedPayload>>()
                .Unsubscribe(m_GraphCompilationUpdatedSubscriptionToken);
        }

        private void CalculateRiskMetrics()
        {
            lock (m_Lock)
            {
                ClearRiskMetrics();
                IEnumerable<IDependentActivity<int, int>> dependentActivities = GraphCompilation?.DependentActivities;
                if (dependentActivities != null
                    && dependentActivities.Any())
                {
                    if (HasCompilationErrors)
                    {
                        return;
                    }
                    Metrics = m_ProjectService.CalculateProjectMetrics(
                        m_Mapper.Map<IEnumerable<IActivity<int, int>>, IList<ActivityModel>>(dependentActivities.Where(x => !x.IsDummy).Select(x => (IActivity<int, int>)x)),
                        ArrowGraphSettings?.ActivitySeverities);
                    SetRiskMetrics();
                }
            }
        }

        private void ClearRiskMetrics()
        {
            lock (m_Lock)
            {
                CriticalityRisk = null;
                FibonacciRisk = null;
                ActivityRisk = null;
                ActivityRiskWithStdDevCorrection = null;
                GeometricCriticalityRisk = null;
                GeometricFibonacciRisk = null;
                GeometricActivityRisk = null;
            }
        }

        private void SetRiskMetrics()
        {
            lock (m_Lock)
            {
                ClearRiskMetrics();
                MetricsModel metrics = Metrics;
                if (metrics != null)
                {
                    CriticalityRisk = metrics.Criticality;
                    FibonacciRisk = metrics.Fibonacci;
                    ActivityRisk = metrics.Activity;
                    ActivityRiskWithStdDevCorrection = metrics.ActivityStdDevCorrection;
                    GeometricCriticalityRisk = metrics.GeometricCriticality;
                    GeometricFibonacciRisk = metrics.GeometricFibonacci;
                    GeometricActivityRisk = metrics.GeometricActivity;
                }
            }
        }

        #endregion

        #region IMetricsManagerViewModel Members

        public bool IsBusy
        {
            get
            {
                return m_CoreViewModel.IsBusy;
            }
            private set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.IsBusy = value;
                }
                RaisePropertyChanged();
            }
        }

        public bool HasCompilationErrors => m_CoreViewModel.HasCompilationErrors;

        public bool HasStaleOutputs => m_CoreViewModel.HasStaleOutputs;

        public double? CriticalityRisk
        {
            get
            {
                return m_CriticalityRisk;
            }
            private set
            {
                m_CriticalityRisk = value;
                RaisePropertyChanged();
            }
        }

        public double? FibonacciRisk
        {
            get
            {
                return m_FibonacciRisk;
            }
            private set
            {
                m_FibonacciRisk = value;
                RaisePropertyChanged();
            }
        }

        public double? ActivityRisk
        {
            get
            {
                return m_ActivityRisk;
            }
            private set
            {
                m_ActivityRisk = value;
                RaisePropertyChanged();
            }
        }

        public double? ActivityRiskWithStdDevCorrection
        {
            get
            {
                return m_ActivityRiskWithStdDevCorrection;
            }
            private set
            {
                m_ActivityRiskWithStdDevCorrection = value;
                RaisePropertyChanged();
            }
        }

        public double? GeometricCriticalityRisk
        {
            get
            {
                return m_GeometricCriticalityRisk;
            }
            private set
            {
                m_GeometricCriticalityRisk = value;
                RaisePropertyChanged();
            }
        }

        public double? GeometricFibonacciRisk
        {
            get
            {
                return m_GeometricFibonacciRisk;
            }
            private set
            {
                m_GeometricFibonacciRisk = value;
                RaisePropertyChanged();
            }
        }

        public double? GeometricActivityRisk
        {
            get
            {
                return m_GeometricActivityRisk;
            }
            private set
            {
                m_GeometricActivityRisk = value;
                RaisePropertyChanged();
            }
        }

        public int? CyclomaticComplexity
        {
            get
            {
                return m_CoreViewModel.CyclomaticComplexity;
            }
            private set
            {
                m_CoreViewModel.CyclomaticComplexity = value;
                RaisePropertyChanged();
            }
        }

        public double? DurationManMonths
        {
            get
            {
                return m_CoreViewModel.DurationManMonths;
            }
            private set
            {
                m_CoreViewModel.DurationManMonths = value;
                RaisePropertyChanged();
            }
        }

        public double? DirectCost => m_CoreViewModel.DirectCost;

        public double? IndirectCost => m_CoreViewModel.IndirectCost;

        public double? OtherCost => m_CoreViewModel.OtherCost;

        public double? TotalCost => m_CoreViewModel.TotalCost;

        public double? Efficiency => m_CoreViewModel.Efficiency;

        #endregion
    }
}
