using GraphX.Controls;
using GraphX.PCL.Common.Interfaces;
using Prism;
using Prism.Events;
using QuickGraph;
using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public partial class ArrowGraphManagerView
        : IActiveAware
    {
        #region Fields

        private bool m_IsActive;
        private readonly IFileDialogService m_FileDialogService;
        private readonly IProjectSettingService m_ProjectSettingService;
        private readonly IEventAggregator m_EventService;
        private SubscriptionToken m_ArrowGraphDataUpdatedSubscriptionToken;

        #endregion

        #region Ctors

        public ArrowGraphManagerView(
            IArrowGraphManagerViewModel viewModel,
            IFileDialogService fileDialogService,
            IProjectSettingService projectSettingService,
            IEventAggregator eventService)
        {
            m_FileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            m_ProjectSettingService = projectSettingService ?? throw new ArgumentNullException(nameof(projectSettingService));
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
            InitializeComponent();
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            ArrowGraphAreaCtrl.ShowAllEdgesLabels();
            ArrowGraphAreaCtrl.SetVerticesDrag(true);
            SubscribeToEvents();
        }

        #endregion

        #region Properties

        public IArrowGraphManagerViewModel ViewModel
        {
            get
            {
                return DataContext as IArrowGraphManagerViewModel;
            }
            set
            {
                DataContext = value;
            }
        }

        #endregion

        #region Private Methods

        private void SubscribeToEvents()
        {
            m_ArrowGraphDataUpdatedSubscriptionToken =
                m_EventService.GetEvent<PubSubEvent<ArrowGraphDataUpdatedPayload>>()
                .Subscribe(payload =>
                {
                    ArrowGraphAreaCtrl.ClearLayout();
                    ArrowGraphData arrowGraphData = ViewModel.ArrowGraphData;
                    if (arrowGraphData != null)
                    {
                        ArrowGraphAreaCtrl.GenerateGraph(arrowGraphData);
                        ResetGraph();
                    }
                }, ThreadOption.UIThread);
        }

        private void UnsubscribeFromEvents()
        {
            m_EventService.GetEvent<PubSubEvent<ArrowGraphDataUpdatedPayload>>()
                .Unsubscribe(m_ArrowGraphDataUpdatedSubscriptionToken);
        }

        private void ResetGraph()
        {
            IGXLogicCore<ArrowGraphVertex, ArrowGraphEdge, BidirectionalGraph<ArrowGraphVertex, ArrowGraphEdge>> logicCore = ArrowGraphAreaCtrl.LogicCore;
            if (logicCore == null)
            {
                throw new InvalidOperationException("LogicCore is null");
            }
            BidirectionalGraph<ArrowGraphVertex, ArrowGraphEdge> graph = logicCore.Graph;
            logicCore.ExternalLayoutAlgorithm = new ArrowGraphLayoutAlgorithm<ArrowGraphVertex, ArrowGraphEdge, BidirectionalGraph<ArrowGraphVertex, ArrowGraphEdge>>(graph);
            ArrowGraphAreaCtrl.RelayoutGraph();
            ArrowGraphAreaCtrl.GenerateAllEdges();
            logicCore.ExternalLayoutAlgorithm = null;
            ZoomCtrl.ZoomToFill();
            ZoomCtrl.Mode = ZoomControlModes.Custom;
        }

        private void ResetGraph_Click(object sender, RoutedEventArgs e)
        {
            ResetGraph();
        }

        private void ExportGraphML_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string directory = m_ProjectSettingService.PlanDirectory;
                if (m_FileDialogService.ShowSaveDialog(
                    directory,
                    Properties.Resources.Filter_SaveGraphMLFileType,
                    Properties.Resources.Filter_SaveGraphMLFileExtension) == DialogResult.OK)
                {
                    {
                        string filename = m_FileDialogService.Filename;
                        if (string.IsNullOrWhiteSpace(filename))
                        {
                            System.Windows.MessageBox.Show(
                                Properties.Resources.Message_EmptyFilename,
                                Properties.Resources.Title_Error,
                                MessageBoxButton.OKCancel,
                                MessageBoxImage.Error);
                        }
                        else
                        {
                            File.WriteAllBytes(
                                filename,
                                ViewModel.ExportArrowGraphToDiagram(
                                    ArrowGraphAreaCtrl.ToDiagramArrowGraphDto()));
                            m_ProjectSettingService.SetDirectory(filename);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    ex.Message,
                    Properties.Resources.Title_Error,
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Error);
            }
        }

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
