using Prism.Commands;
using Prism.Events;
using Prism.Interactivity.InteractionRequest;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Zametek.Common.Project;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class GanttChartManagerViewModel
        : PropertyChangedPubSubViewModel, IGanttChartManagerViewModel
    {
        #region Fields

        private readonly object m_Lock;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly IEventAggregator m_EventService;

        private readonly InteractionRequest<Notification> m_NotificationInteractionRequest;

        private SubscriptionToken m_GraphCompiledSubscriptionToken;
        private SubscriptionToken m_ArrowGraphSettingsUpdatedSubscriptionToken;
        private SubscriptionToken m_GanttChartSettingsUpdatedSubscriptionToken;
        private SubscriptionToken m_GanttChartDtoUpdatedSubscriptionToken;

        #endregion

        #region Ctors

        public GanttChartManagerViewModel(
            ICoreViewModel coreViewModel,
            IDateTimeCalculator dateTimeCalculator,
            IEventAggregator eventService)
            : base(eventService)
        {
            m_Lock = new object();
            m_CoreViewModel = coreViewModel ?? throw new ArgumentNullException(nameof(coreViewModel));
            m_DateTimeCalculator = dateTimeCalculator ?? throw new ArgumentNullException(nameof(dateTimeCalculator));
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

        private GraphCompilation<int, IDependentActivity<int>> GraphCompilation => m_CoreViewModel.GraphCompilation;

        private IList<ResourceSeriesDto> ResourceSeriesSet => m_CoreViewModel.ResourceSeriesSet;

        #endregion

        #region Commands

        private DelegateCommandBase InternalGenerateGanttChartCommand
        {
            get;
            set;
        }

        private async void GenerateGanttChart()
        {
            await DoGenerateGanttChartAsync();
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
                await GenerateGanttChartFromGraphCompilationAsync();
                HasStaleGanttChart = false;
                //IsProjectUpdated = true;
            }
            catch (Exception ex)
            {
                DispatchNotification(
                    Properties.Resources.Title_Error,
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
            m_GanttChartDtoUpdatedSubscriptionToken =
                 m_EventService.GetEvent<PubSubEvent<GanttChartDtoUpdatedPayload>>()
                     .Subscribe(async payload =>
                     {
                         await GenerateGanttChartFromGraphCompilationAsync();
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
            m_EventService.GetEvent<PubSubEvent<GanttChartDtoUpdatedPayload>>()
                .Unsubscribe(m_GanttChartDtoUpdatedSubscriptionToken);
        }

        private void PublishGanttChartDataUpdatedPayload()
        {
            m_EventService.GetEvent<PubSubEvent<GanttChartDataUpdatedPayload>>()
                .Publish(new GanttChartDataUpdatedPayload());
        }

        private async Task GenerateGanttChartFromGraphCompilationAsync()
        {
            await Task.Run(() => GenerateGanttChartFromGraphCompilation());
        }

        private void GenerateGanttChartFromGraphCompilation()
        {
            lock (m_Lock)
            {
                GanttChartDto = null;
                IList<IDependentActivity<int>> dependentActivities =
                    GraphCompilation.DependentActivities
                    .Select(x => (IDependentActivity<int>)x.WorkingCopy())
                    .ToList();

                if (!HasCompilationErrors
                    && dependentActivities.Any())
                {
                    IList<IDependentActivity<int>> orderedActivities =
                        dependentActivities.OrderBy(x => x.EarliestStartTime)
                        .ThenBy(x => x.Duration)
                        .ToList();

                    ArrowGraphSettingsDto arrowGraphSettings = ArrowGraphSettingsDto;
                    IList<IResourceSchedule<int>> resourceSchedules = GraphCompilation.ResourceSchedules;
                    IList<ResourceSeriesDto> resourceSeriesSet = ResourceSeriesSet;

                    if (arrowGraphSettings != null
                        && resourceSchedules != null
                        && resourceSeriesSet != null)
                    {
                        GanttChartDto = new GanttChartDto
                        {
                            DependentActivities = orderedActivities,
                            ResourceSchedules = resourceSchedules,
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
                        && GanttChartDto != null)
                    {
                        GanttChartDto.IsStale = true;
                    }
                    return GanttChartDto?.IsStale ?? false;
                }
            }
            private set
            {
                lock (m_Lock)
                {
                    if (GanttChartDto != null)
                    {
                        GanttChartDto.IsStale = value;
                    }
                }
                RaisePropertyChanged();
            }
        }

        public GanttChartDto GanttChartDto { get; private set; }

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

        public ArrowGraphSettingsDto ArrowGraphSettingsDto => m_CoreViewModel.ArrowGraphSettingsDto;

        #endregion
    }
}
