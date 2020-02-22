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

        private SubscriptionToken m_GraphCompilationUpdatedPayloadToken;

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

            ArrangedActivities = new List<ManagedActivityViewModel>();

            m_NotificationInteractionRequest = new InteractionRequest<Notification>();

            InitializeCommands();
            SubscribeToEvents();

            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.ProjectStart), nameof(ProjectStart), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.IsBusy), nameof(IsBusy), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.HasStaleOutputs), nameof(HasStaleOutputs), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.HasCompilationErrors), nameof(HasCompilationErrors), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.ShowDates), nameof(ShowDates), ThreadOption.BackgroundThread);
            SubscribePropertyChanged(m_CoreViewModel, nameof(m_CoreViewModel.ShowDates), nameof(ShowDays), ThreadOption.BackgroundThread);
        }

        #endregion

        #region Properties

        private ObservableCollection<ManagedActivityViewModel> Activities => m_CoreViewModel.Activities;

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
                await GenerateGanttChartDataFromGraphCompilationAsync();
                //HasStaleArrowGraph = false;
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



            //m_GraphCompiledPayloadToken =
            //    m_EventService.GetEvent<PubSubEvent<GraphCompiledPayload>>()
            //        .Subscribe(payload =>
            //        {
            //            HasStaleArrowGraph = true;
            //        }, ThreadOption.BackgroundThread);
            //m_ArrowGraphSettingsUpdatedPayloadToken =
            //    m_EventService.GetEvent<PubSubEvent<ArrowGraphSettingsUpdatedPayload>>()
            //        .Subscribe(payload =>
            //        {
            //            HasStaleArrowGraph = true;
            //        }, ThreadOption.BackgroundThread);
            //m_ArrowGraphDtoUpdatedSubscriptionToken =
            //    m_EventService.GetEvent<PubSubEvent<ArrowGraphDtoUpdatedPayload>>()
            //        .Subscribe(async payload =>
            //        {
            //            await GenerateArrowGraphDataFromDtoAsync();
            //        }, ThreadOption.BackgroundThread);









            //m_GraphCompilationUpdatedPayloadToken =
            //    m_EventService.GetEvent<PubSubEvent<GraphCompilationUpdatedPayload>>()
            //        .Subscribe(payload =>
            //        {
            //            //HasStaleArrowGraph = true;
            //            //IsBusy = true;
            //            //CalculateGanttChartModel();
            //            //IsBusy = false;
            //        }, ThreadOption.BackgroundThread);
        }

        private void UnsubscribeFromEvents()
        {
            m_EventService.GetEvent<PubSubEvent<GraphCompilationUpdatedPayload>>()
                .Unsubscribe(m_GraphCompilationUpdatedPayloadToken);
        }

        private void PublishGanttChartDataUpdatedPayload()
        {
            m_EventService.GetEvent<PubSubEvent<GanttChartDataUpdatedPayload>>()
                .Publish(new GanttChartDataUpdatedPayload());
        }

        private async Task GenerateGanttChartDataFromGraphCompilationAsync()
        {
            await Task.Run(() => GenerateGanttChartDataFromGraphCompilation());
        }

        private void GenerateGanttChartDataFromGraphCompilation()
        {
            lock (m_Lock)
            {
                ArrangedActivities.Clear();
                ArrangedActivities.AddRange(Activities.OrderBy(x => x.EarliestStartTime).ThenBy(x => x.Duration));
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

        public bool HasStaleOutputs
        {
            get
            {
                return m_CoreViewModel.HasStaleOutputs;
            }
            private set
            {
                lock (m_Lock)
                {
                    m_CoreViewModel.HasStaleOutputs = value;
                }
                RaisePropertyChanged();
            }
        }

        public bool ShowDates => m_CoreViewModel.ShowDates;

        public bool ShowDays => !ShowDates;

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

        public List<ManagedActivityViewModel> ArrangedActivities { get; }

        public ICommand GenerateGanttChartCommand
        {
            get;
            private set;
        }

        #endregion
    }
}
