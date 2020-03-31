using Prism.Events;
using System;
using System.ComponentModel;
using System.Windows;
using Zametek.Contract.ProjectPlan;
using Zametek.Event.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class MainView
        : Window
    {
        #region Fields

        private readonly IEventAggregator m_EventService;

        #endregion

        #region Ctors

        public MainView(
            IMainViewModel viewModel,
            IEventAggregator eventService)
        {
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
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

        #region Overrides

        protected override void OnClosing(CancelEventArgs e)
        {
            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }

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
