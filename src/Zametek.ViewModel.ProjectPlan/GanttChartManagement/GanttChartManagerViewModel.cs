using AutoMapper;
using Prism;
using Prism.Commands;
using Prism.Events;
using Prism.Interactivity.InteractionRequest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Event.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class GanttChartManagerViewModel
        : PropertyChangedPubSubViewModel, IGanttChartManagerViewModel, IActiveAware
    {
        #region Fields

        private readonly object m_Lock;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IMapper m_Mapper;
        private readonly IEventAggregator m_EventService;

        private readonly InteractionRequest<Notification> m_NotificationInteractionRequest;

        private SubscriptionToken m_GraphCompiledSubscriptionToken;
        private SubscriptionToken m_ArrowGraphSettingsUpdatedSubscriptionToken;
        private SubscriptionToken m_GanttChartSettingsUpdatedSubscriptionToken;
        private SubscriptionToken m_GanttChartUpdatedSubscriptionToken;

        private bool m_IsActive;

        #endregion

        #region Ctors

        public GanttChartManagerViewModel(
            ICoreViewModel coreViewModel,
            IMapper mapper,
            IEventAggregator eventService)
            : base(eventService)
        {
            m_Lock = new object();
            m_CoreViewModel = coreViewModel ?? throw new ArgumentNullException(nameof(coreViewModel));
            m_Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));

            m_NotificationInteractionRequest = new InteractionRequest<Notification>();

            InitializeCommands();
            SubscribeToEvents();

            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.ProjectStart), nameof(ProjectStart), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.IsBusy), nameof(IsBusy), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.HasStaleOutputs), nameof(HasStaleGanttChart), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.HasCompilationErrors), nameof(HasCompilationErrors), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.UseBusinessDays), nameof(UseBusinessDays), ThreadOption.BackgroundThread);
        }

        #endregion

        #region Properties

        private bool HasStaleOutputs => m_CoreViewModel.HasStaleOutputs;

        private IGraphCompilation<int, int, IDependentActivity<int, int>> GraphCompilation => m_CoreViewModel.GraphCompilation;

        private ResourceSeriesSetModel ResourceSeriesSet => m_CoreViewModel.ResourceSeriesSet;

        #endregion

        #region Commands

        private DelegateCommandBase InternalGenerateGanttChartCommand
        {
            get;
            set;
        }

        private async void GenerateGanttChart()
        {
            await DoGenerateGanttChartAsync().ConfigureAwait(true);
        }

        private bool CanGenerateGanttChart()
        {
            return !HasCompilationErrors;
        }

        #endregion

        #region Public Methods

        public async Task DoGenerateGanttChartAsync()
        {
            try
            {
                IsBusy = true;
                await GenerateGanttChartFromGraphCompilationAsync().ConfigureAwait(true);
                HasStaleGanttChart = false;
                //IsProjectUpdated = true;
            }
            catch (Exception ex)
            {
                DispatchNotification(
                    Resource.ProjectPlan.Resources.Title_Error,
                    ex.Message);
            }
            finally
            {
                IsBusy = false;
                RaiseCanExecuteChangedAllCommands();
            }
        }

        #endregion

        #region Private Methods

        private void InitializeCommands()
        {
            GenerateGanttChartCommand =
                InternalGenerateGanttChartCommand =
                    new DelegateCommand(GenerateGanttChart, CanGenerateGanttChart);
        }

        private void RaiseCanExecuteChangedAllCommands()
        {
            InternalGenerateGanttChartCommand.RaiseCanExecuteChanged();
        }

        private void SubscribeToEvents()
        {
            m_GraphCompiledSubscriptionToken =
                m_EventService.GetEvent<PubSubEvent<GraphCompiledPayload>>()
                    .Subscribe(payload =>
                    {
                        HasStaleGanttChart = true;
                    }, ThreadOption.BackgroundThread);
            m_ArrowGraphSettingsUpdatedSubscriptionToken =
                m_EventService.GetEvent<PubSubEvent<ArrowGraphSettingsUpdatedPayload>>()
                    .Subscribe(payload =>
                    {
                        HasStaleGanttChart = true;
                    }, ThreadOption.BackgroundThread);
            m_GanttChartSettingsUpdatedSubscriptionToken =
                m_EventService.GetEvent<PubSubEvent<GanttChartSettingsUpdatedPayload>>()
                    .Subscribe(payload =>
                    {
                        HasStaleGanttChart = true;
                    }, ThreadOption.BackgroundThread);
            m_GanttChartUpdatedSubscriptionToken =
                 m_EventService.GetEvent<PubSubEvent<GanttChartUpdatedPayload>>()
                     .Subscribe(async payload =>
                     {
                         await GenerateGanttChartFromGraphCompilationAsync().ConfigureAwait(true);
                     }, ThreadOption.BackgroundThread);
        }

        private void UnsubscribeFromEvents()
        {
            m_EventService.GetEvent<PubSubEvent<GraphCompiledPayload>>()
                .Unsubscribe(m_GraphCompiledSubscriptionToken);
            m_EventService.GetEvent<PubSubEvent<ArrowGraphSettingsUpdatedPayload>>()
                .Unsubscribe(m_ArrowGraphSettingsUpdatedSubscriptionToken);
            m_EventService.GetEvent<PubSubEvent<GanttChartSettingsUpdatedPayload>>()
                .Unsubscribe(m_GanttChartSettingsUpdatedSubscriptionToken);
            m_EventService.GetEvent<PubSubEvent<GanttChartUpdatedPayload>>()
                .Unsubscribe(m_GanttChartUpdatedSubscriptionToken);
        }

        private void PublishGanttChartDataUpdatedPayload()
        {
            m_EventService.GetEvent<PubSubEvent<GanttChartDataUpdatedPayload>>()
                .Publish(new GanttChartDataUpdatedPayload());
        }

        private async Task GenerateGanttChartFromGraphCompilationAsync()
        {
            await Task.Run(() => GenerateGanttChartFromGraphCompilation()).ConfigureAwait(true);
        }

        private void GenerateGanttChartFromGraphCompilation()
        {
            lock (m_Lock)
            {
                GanttChart = null;
                IList<IDependentActivity<int, int>> dependentActivities =
                    GraphCompilation.DependentActivities
                    .Select(x => (IDependentActivity<int, int>)x.CloneObject())
                    .ToList();

                if (!HasCompilationErrors
                    && dependentActivities.Any())
                {
                    List<IDependentActivity<int, int>> orderedActivities =
                        dependentActivities.OrderBy(x => x.EarliestStartTime)
                        .ThenBy(x => x.Duration)
                        .ToList();

                    ArrowGraphSettingsModel arrowGraphSettings = ArrowGraphSettings;
                    ResourceSeriesSetModel resourceSeriesSet = ResourceSeriesSet;

                    if (arrowGraphSettings != null
                        && resourceSeriesSet != null)
                    {
                        GanttChart = new GanttChartModel
                        {
                            DependentActivities = m_Mapper.Map<List<IDependentActivity<int, int>>, List<DependentActivityModel>>(orderedActivities),
                            ResourceSeriesSet = resourceSeriesSet,
                            IsStale = false,
                        };
                    }
                }
            }
            PublishGanttChartDataUpdatedPayload();
        }

        private void DispatchNotification(string title, object content)
        {
            m_NotificationInteractionRequest.Raise(
                new Notification
                {
                    Title = title,
                    Content = content
                });
        }

        #endregion

        #region IGanttChartManagerViewModel Members

        public string Title => Resource.ProjectPlan.Resources.Label_GanttChartViewTitle;

        public IInteractionRequest NotificationInteractionRequest => m_NotificationInteractionRequest;

        public DateTime ProjectStart => m_CoreViewModel.ProjectStart;

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

        public bool HasStaleGanttChart
        {
            get
            {
                lock (m_Lock)
                {
                    if (HasStaleOutputs
                        && GanttChart != null)
                    {
                        GanttChart.IsStale = true;
                    }
                    return GanttChart?.IsStale ?? false;
                }
            }
            private set
            {
                lock (m_Lock)
                {
                    if (GanttChart != null)
                    {
                        GanttChart.IsStale = value;
                    }
                }
                RaisePropertyChanged();
            }
        }

        public GanttChartModel GanttChart { get; private set; }

        public bool UseBusinessDays => m_CoreViewModel.UseBusinessDays;

        public bool HasCompilationErrors
        {
            get
            {
                return m_CoreViewModel.HasCompilationErrors;
            }
            private set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.HasCompilationErrors = value;
                }
                RaisePropertyChanged();
            }
        }

        public ICommand GenerateGanttChartCommand
        {
            get;
            private set;
        }

        public ArrowGraphSettingsModel ArrowGraphSettings => m_CoreViewModel.ArrowGraphSettings;

        #endregion

        #region IActiveAware Members

        public event EventHandler IsActiveChanged;

        public bool IsActive
        {
            get
            {
                return m_IsActive;
            }
            set
            {
                if (m_IsActive != value)
                {
                    m_IsActive = value;
                    IsActiveChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        #endregion
    }
}
