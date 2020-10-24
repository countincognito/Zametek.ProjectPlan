using Prism.Events;
using System;
using System.ComponentModel;
using System.Windows;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Event.ProjectPlan;
using Zametek.Wpf.Core;

namespace Zametek.View.ProjectPlan
{
    public partial class MainView
        : Window
    {
        #region Fields

        private readonly IActivitiesManagerViewModel m_ActivitiesManagerViewModel;
        private readonly IGanttChartManagerViewModel m_GanttChartManagerViewModel;
        private readonly IArrowGraphManagerViewModel m_ArrowGraphManagerViewModel;
        private readonly IResourceChartManagerViewModel m_ResourceChartManagerViewModel;
        private readonly IEarnedValueChartManagerViewModel m_EarnedValueChartManagerViewModel;
        private readonly IEventAggregator m_EventService;
        private readonly ISettingService m_settingService;

        #endregion

        #region Ctors

        public MainView(
            IMainViewModel viewModel,
            IActivitiesManagerViewModel activitiesManagerViewModel,
            IGanttChartManagerViewModel ganttChartManagerViewModel,
            IArrowGraphManagerViewModel arrowGraphManagerViewModel,
            IResourceChartManagerViewModel resourceChartManagerViewModel,
            IEarnedValueChartManagerViewModel earnedValueChartManagerViewModel,
            IEventAggregator eventService,
            ISettingService settingService)
        {
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            m_ActivitiesManagerViewModel = activitiesManagerViewModel ?? throw new ArgumentNullException(nameof(activitiesManagerViewModel));
            m_GanttChartManagerViewModel = ganttChartManagerViewModel ?? throw new ArgumentNullException(nameof(ganttChartManagerViewModel));
            m_ArrowGraphManagerViewModel = arrowGraphManagerViewModel ?? throw new ArgumentNullException(nameof(arrowGraphManagerViewModel));
            m_ResourceChartManagerViewModel = resourceChartManagerViewModel ?? throw new ArgumentNullException(nameof(resourceChartManagerViewModel));
            m_EarnedValueChartManagerViewModel = earnedValueChartManagerViewModel ?? throw new ArgumentNullException(nameof(earnedValueChartManagerViewModel));
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
            m_settingService = settingService ?? throw new ArgumentNullException(nameof(settingService));
            InitializeComponent();
        }

        #endregion

        #region Properties

        public IMainViewModel ViewModel
        {
            get
            {
                return DataContext as IMainViewModel;
            }
            set
            {
                DataContext = value;
            }
        }

        #endregion

        #region Private Methods

        private void MenuItem_ViewActivitiesManager(object sender, RoutedEventArgs e)
        {
            DockManager.ShowAnchorable(m_ActivitiesManagerViewModel, setAsActiveContent: true);
        }

        private void MenuItem_ViewGanttChartManager(object sender, RoutedEventArgs e)
        {
            DockManager.ShowAnchorable(m_GanttChartManagerViewModel, setAsActiveContent: true);
        }

        private void MenuItem_ViewArrowGraphManager(object sender, RoutedEventArgs e)
        {
            DockManager.ShowAnchorable(m_ArrowGraphManagerViewModel, setAsActiveContent: true);
        }

        private void MenuItem_ViewResourceChartManager(object sender, RoutedEventArgs e)
        {
            DockManager.ShowAnchorable(m_ResourceChartManagerViewModel, setAsActiveContent: true);
        }

        private void MenuItem_ViewEarnedValueChartManager(object sender, RoutedEventArgs e)
        {
            DockManager.ShowAnchorable(m_EarnedValueChartManagerViewModel, setAsActiveContent: true);
        }

        #endregion

        #region Overrides

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var settings = m_settingService.MainViewSettings;

            WindowState = settings.Maximized ? WindowState.Maximized : WindowState.Normal;
            Top = settings.Top;
            Left = settings.Left;
            Width = settings.Width;
            Height = settings.Height;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            var settings = new MainViewSettingsModel
            {
                Maximized = WindowState == WindowState.Maximized,
                Top = Top,
                Left = Left,
                Width = Width,
                Height = Height
            };
            m_settingService.SetMainViewSettings(settings);

            base.OnClosing(e);
            var closingPayload = new ApplicationClosingPayload();
            m_EventService.GetEvent<PubSubEvent<ApplicationClosingPayload>>()
                .Publish(closingPayload);

            // User canceled the closing of the application.
            e.Cancel = closingPayload.IsCanceled;
        }

        #endregion
    }
}
