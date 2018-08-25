using Prism.Events;
using System;
using System.ComponentModel;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public partial class MainView
    {
        #region Fields

        private readonly IEventAggregator m_EventService;

        #endregion

        #region Ctors

        public MainView(
            IMainViewModel viewModel,
            IEventAggregator eventService)
        {
            InitializeComponent();
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
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
