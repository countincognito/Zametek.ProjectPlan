using DynamicData;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ProjectManagerViewModel
        : ToolViewModelBase, IProjectManagerViewModel, IDisposable
    {
        #region Fields

        private readonly object m_Lock;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IDialogService m_DialogService;
        private Dictionary<Guid, IManagedPlanViewModel> m_ManagedPlanLookup;
        private Dictionary<Guid, List<string>> m_BranchLabelLookup;

        #endregion

        #region Ctors

        public ProjectManagerViewModel(
            ICoreViewModel coreViewModel,
            IDialogService dialogService)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(dialogService);
            m_Lock = new object();
            m_CoreViewModel = coreViewModel;
            m_DialogService = dialogService;
            m_Plans = new();
            m_ManagedPlanLookup = [];
            m_BranchLabelLookup = [];
            Root = null;

            // Create read-only view to the source list.
            m_Plans.Connect()
               .ObserveOn(RxApp.MainThreadScheduler)
               .Bind(out m_ReadOnlyPlans)
               .Subscribe();

            Id = Resource.ProjectPlan.Titles.Title_Project;
            Title = Resource.ProjectPlan.Titles.Title_Project;
        }

        #endregion

        #region Properties

        #endregion

        #region Private Methods

        #endregion

        #region IProjectManagerViewModel

        public IManagedPlanViewModel? Root { get; private set; }

        private readonly SourceList<IManagedPlanViewModel> m_Plans;
        private readonly ReadOnlyObservableCollection<IManagedPlanViewModel> m_ReadOnlyPlans;
        public ReadOnlyObservableCollection<IManagedPlanViewModel> Plans => m_ReadOnlyPlans;

        public void ResetProject()
        {
            try
            {
                lock (m_Lock)
                {
                    //IsBusy = true;
                    //m_TrackIsProjectUpdated = false;
                    //m_TrackHasStaleOutputs = false;

                    Root?.ClearChildren();
                    m_ManagedPlanLookup.Clear();
                    m_BranchLabelLookup.Clear();

                    Root = null;

                    m_Plans.Clear();

                    //ClearSettings();

                    //Metrics = new();

                    //HasCompilationErrors = false;
                    //GraphCompilation = new GraphCompilation<int, int, int, DependentActivity>([], [], []);

                    //ArrowGraph = new();
                    //VertexGraph = new();

                    //IsReadyToCompile = ReadyToCompile.No;
                    //IsReadyToReviseTrackers = ReadyToRevise.No;
                    //IsReadyToReviseSettings = ReadyToRevise.No;

                    //m_SettingService.Reset();

                    //m_TrackIsProjectUpdated = true;
                    //IsProjectUpdated = false;

                    //m_TrackHasStaleOutputs = true;
                    //HasStaleOutputs = false;
                }
            }
            finally
            {
                //m_TrackIsProjectUpdated = true;
                //m_TrackHasStaleOutputs = true;
                //IsBusy = false;
            }
        }


        public void ProcessProject(ProjectModel projectModel)
        {
            try
            {
                lock (m_Lock)
                {
                    //IsBusy = true;
                    ResetProject();

                    foreach (ProjectPlanBranchModel planBranch in projectModel.Branches)
                    {
                        if (!m_BranchLabelLookup.TryGetValue(planBranch.NodeId, out List<string>? labels))
                        {
                            labels = [];
                            m_BranchLabelLookup.Add(planBranch.NodeId, labels);
                        }

                        labels.Add(planBranch.Label);
                    }

                    // Root node.
                    Root = new ManagedPlanViewModel(
                        new ProjectPlanNodeModel
                        {
                            Id = projectModel.Root,
                        });

                    if (m_BranchLabelLookup.TryGetValue(Root.Id, out List<string>? rootLabels))
                    {
                        Root.SetLabels(rootLabels);
                    }



                    m_Plans.Add(Root);

                    // Plans.
                    AddManagedPlans(projectModel.Nodes);

                }
            }
            finally
            {
                //m_TrackIsProjectUpdated = true;
                //m_TrackHasStaleOutputs = true;
                //IsBusy = false;
            }
        }

        public ProjectModel BuildProject()
        {
            return null;
        }























        public void AddManagedPlans(IEnumerable<ProjectPlanNodeModel> projectPlanNodeModels)
        {
            try
            {
                lock (m_Lock)
                {
                    if (Root is not null)
                    {
                        foreach (ProjectPlanNodeModel projectPlanNode in projectPlanNodeModels)
                        {







                            if (!m_ManagedPlanLookup.ContainsKey(projectPlanNode.Id))
                            {
                                var projectPlan = new ManagedPlanViewModel(projectPlanNode);

                                if (m_BranchLabelLookup.TryGetValue(projectPlan.Id, out List<string>? labels))
                                {
                                    projectPlan.SetLabels(labels);
                                }

                                m_ManagedPlanLookup.Add(projectPlan.Id, projectPlan);



                                if (projectPlan.ParentId == Root.Id)
                                {
                                    // Top-level plan.
                                    Root.AddChildren([projectPlan]);
                                }
                                else if (m_ManagedPlanLookup.TryGetValue(projectPlan.ParentId, out IManagedPlanViewModel? parentPlan))
                                {
                                    // Child plan.
                                    parentPlan.AddChildren([projectPlan]);
                                }
                                else
                                {
                                    // Orphaned plan - treat as top-level.
                                    //plans.Add(projectPlan);
                                    projectPlan.Dispose();

                                    throw new Exception(projectPlanNode.Id + ": Unable to add managed plan - parent plan not found." + projectPlanNode.ParentId);
                                }








                            }


                        }




                        //foreach (DependentActivityModel dependentActivity in dependentActivityModels)
                        //{
                        //    var activity = new ManagedActivityViewModel(
                        //        this,
                        //        m_Mapper.Map<DependentActivityModel, DependentActivity>(dependentActivity),
                        //        m_DateTimeCalculator,
                        //        m_VertexGraphCompiler,
                        //        ProjectStart,
                        //        dependentActivity.Activity.Trackers,
                        //        dependentActivity.Activity.MinimumEarliestStartDateTime,
                        //        dependentActivity.Activity.MaximumLatestFinishDateTime);

                        //    if (m_VertexGraphCompiler.AddActivity(activity))
                        //    {
                        //        activities.Add(activity);
                        //    }
                        //    else
                        //    {
                        //        activity.Dispose();
                        //    }
                        //}


                        //IsProjectUpdated = true;
                    }
                }

            }
            finally
            {
            }
        }

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
        }

        #endregion

        #region IDisposable Members

        private bool m_Disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (m_Disposed)
            {
                return;
            }

            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
                ResetProject();
                KillSubscriptions();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

            m_Disposed = true;
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
