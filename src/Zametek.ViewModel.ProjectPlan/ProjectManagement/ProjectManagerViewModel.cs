using DynamicData;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
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
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;
        private Dictionary<Guid, IManagedPlanViewModel> m_ManagedPlanLookup;
        private Dictionary<Guid, List<string>> m_PlanTagLookup;

        #endregion

        #region Ctors

        public ProjectManagerViewModel(
            ICoreViewModel coreViewModel,
            ISettingService settingService,
            IDialogService dialogService)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dialogService);
            m_Lock = new object();
            m_CoreViewModel = coreViewModel;
            m_SettingService = settingService;
            m_DialogService = dialogService;
            m_IsBusy = false;
            Root = new ManagedPlanViewModel(); // Placeholder until ResetRootNode is called.
            m_Plans = new();
            SelectedPlans = [];
            m_ManagedPlanLookup = [];
            m_PlanTagLookup = [];

            {
                ReactiveCommand<Unit, Unit> loadProjectPlanFileCommand = ReactiveCommand.CreateFromTask(LoadProjectPlanFileAsync);
                loadProjectPlanFileCommand.IsExecuting.ToProperty(this, pm => pm.IsLoading, out m_IsLoading);
                LoadProjectPlanFileCommand = loadProjectPlanFileCommand;
            }

            // Create read-only view to the source list.
            m_Plans.Connect()
               .ObserveOn(RxApp.MainThreadScheduler)
               .Bind(out m_ReadOnlyPlans)
               .Subscribe();

            ResetRootNode();

            Id = Resource.ProjectPlan.Titles.Title_Project;
            Title = Resource.ProjectPlan.Titles.Title_Project;
        }

        #endregion

        #region Properties

        #endregion

        #region Private Methods

        private void ResetRootNode()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    ResetRootNode(Guid.NewGuid());
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ResetRootNode(Guid rootId)
        {
            try
            {
                lock (m_Lock)
                {
                    // Remember that Root does not go in the m_ManagedPlanLookup.
                    IsBusy = true;
                    if (rootId == Guid.Empty)
                    {
                        rootId = Guid.NewGuid();
                    }

                    // Root node.
                    Root = new ManagedPlanViewModel(
                        new ProjectPlanNodeModel
                        {
                            Id = rootId,
                        });

                    AddTagLabels(
                        [
                            new ProjectPlanTagModel
                            {
                                NodeId = rootId,
                                Label = Resource.ProjectPlan.Labels.Label_RootNode,
                            }
                        ]);

                    SetTagLabels(Root);
                    m_Plans.AddRange(Root.Children);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void AddTagLabels(IEnumerable<ProjectPlanTagModel> projectPlanTagModels)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    foreach (ProjectPlanTagModel tagModel in projectPlanTagModels)
                    {
                        if (!m_PlanTagLookup.TryGetValue(tagModel.NodeId, out List<string>? labels))
                        {
                            labels = [];
                            m_PlanTagLookup.Add(tagModel.NodeId, labels);
                        }

                        labels.Add(tagModel.Label);
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SetTagLabels(IManagedPlanViewModel managedPlan)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    if (m_PlanTagLookup.TryGetValue(managedPlan.Id, out List<string>? labels))
                    {
                        managedPlan.SetLabels(labels);
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ClearProject()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    Root?.ClearChildren();
                    m_ManagedPlanLookup.Clear();
                    m_PlanTagLookup.Clear();
                    m_Plans.Clear();
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        #endregion

        #region IProjectManagerViewModel

        private bool m_IsBusy;
        public bool IsBusy
        {
            get => m_IsBusy;
            private set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_IsBusy, value);
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_IsLoading;
        public bool IsLoading => m_IsLoading.Value;

        public IManagedPlanViewModel Root { get; private set; }

        private readonly SourceList<IManagedPlanViewModel> m_Plans;
        private readonly ReadOnlyObservableCollection<IManagedPlanViewModel> m_ReadOnlyPlans;
        public ReadOnlyObservableCollection<IManagedPlanViewModel> Plans => m_ReadOnlyPlans;



        public ObservableCollection<IManagedPlanViewModel> SelectedPlans { get; }





        public ICommand LoadProjectPlanFileCommand { get; }









        public void ResetProject()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    //m_TrackIsProjectUpdated = false;
                    //m_TrackHasStaleOutputs = false;

                    ClearProject();

                    ResetRootNode();


                    //ClearSettings();

                    //Metrics = new();

                    //HasCompilationErrors = false;
                    //GraphCompilation = new GraphCompilation<int, int, int, DependentActivity>([], [], []);

                    //ArrowGraph = new();
                    //VertexGraph = new();

                    //IsReadyToCompile = ReadyToCompile.No;
                    //IsReadyToReviseTrackers = ReadyToRevise.No;
                    //IsReadyToReviseSettings = ReadyToRevise.No;

                    m_SettingService.Reset();

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
                IsBusy = false;
            }
        }


        public void ProcessProject(ProjectModel projectModel)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    ResetProject();
                    ClearProject();

                    // Root node.
                    ResetRootNode(projectModel.Root);

                    AddTagLabels(projectModel.Tags);

                    AddManagedPlans(projectModel.Nodes);
                }
            }
            finally
            {
                //m_TrackIsProjectUpdated = true;
                //m_TrackHasStaleOutputs = true;
                IsBusy = false;
            }
        }

        public IManagedPlanViewModel? GetProjectPlan(Guid projectPlanId)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    if (m_ManagedPlanLookup.TryGetValue(projectPlanId, out IManagedPlanViewModel? projectPlan))
                    {
                        return projectPlan;
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
            return null;
        }

        public IManagedPlanViewModel? GetProjectPlanParent(Guid projectPlanId)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    if (m_ManagedPlanLookup.TryGetValue(projectPlanId, out IManagedPlanViewModel? projectPlan)
                        && m_ManagedPlanLookup.TryGetValue(projectPlan.ParentId, out IManagedPlanViewModel? parentProjectPlan))
                    {
                        return parentProjectPlan;
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
            return null;
        }



        public ProjectModel BuildProject()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;

                    Guid rootId = Root.Id;
                    List<ProjectPlanNodeModel> nodes = [.. m_ManagedPlanLookup.Values.Select(x => x.Node)];

                    // Filter out any tags that apply to the Root node.
                    List<ProjectPlanTagModel> tags = [.. m_PlanTagLookup
                        .Where(kvp => kvp.Key != rootId)
                        .SelectMany(kvp => kvp.Value.Select(
                            label => new ProjectPlanTagModel
                            {
                                NodeId = kvp.Key,
                                Label = label,
                            }))];

                    return new ProjectModel
                    {
                        Version = Data.ProjectPlan.Versions.ProjectLatest,
                        Root = rootId,
                        Nodes = nodes,
                        Tags = tags,
                    };
                }
            }
            finally
            {

                IsBusy = false;
            }
        }



















        public void AddManagedPlans(IEnumerable<ProjectPlanNodeModel> projectPlanNodeModels)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;

                    if (Root is not null)
                    {
                        foreach (ProjectPlanNodeModel projectPlanNode in projectPlanNodeModels)
                        {
                            if (!m_ManagedPlanLookup.ContainsKey(projectPlanNode.Id))
                            {
                                var projectPlan = new ManagedPlanViewModel(projectPlanNode);

                                SetTagLabels(projectPlan);

                                m_ManagedPlanLookup.Add(projectPlan.Id, projectPlan);

                                if (projectPlan.ParentId == Root.Id)
                                {
                                    // Top-level plan.
                                    Root.AddChildren([projectPlan]);
                                    m_Plans.Add(projectPlan);
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
                IsBusy = false;
            }
        }






        public async Task LoadProjectPlanFileAsync()
        {
            try
            {
                IsBusy = true;


                await Task.Run(() =>
                {

                    IManagedPlanViewModel managedPlan = SelectedPlans.First();

                    ProjectPlanNodeModel latestPlanNodeModel = managedPlan.Node;
                    Guid projectPlanId = latestPlanNodeModel.Id;
                    ProjectPlanModel projectPlanModel = latestPlanNodeModel.ProjectPlan;
                    m_CoreViewModel.ProcessProjectPlan(projectPlanModel, projectPlanId);
                });

                




                //    if (IsProjectUpdated)
                //    {
                //        bool confirmation = await m_DialogService.ShowConfirmationAsync(
                //            Resource.ProjectPlan.Titles.Title_UnsavedChanges,
                //            string.Empty,
                //            Resource.ProjectPlan.Messages.Message_UnsavedChanges);

                //        if (!confirmation)
                //        {
                //            return;
                //        }
                //    }
                //    string directory = m_SettingService.ProjectDirectory;
                //    string? filename = await m_DialogService.ShowOpenFileDialogAsync(directory, s_ProjectFileFilters);
                //    await OpenProjectFileInternalAsync(filename);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
                //ResetProject();
            }
            finally
            {

                IsBusy = false;
            }
        }









        //public void UpdateManagedPlan(ProjectPlanNodeModel projectPlanNodeModel)
        //{
        //    try
        //    {
        //        lock (m_Lock)
        //        {
        //            IsBusy = true;
        //            IManagedPlanViewModel? projectPlan = GetProjectPlan(projectPlanNodeModel.Id);

        //            if (projectPlan is not null)
        //            {
        //                projectPlan.ProjectPlan = projectPlanNodeModel.ProjectPlan;
        //            }





        //        }
        //    }
        //    finally
        //    {

        //        IsBusy = false;
        //    }
        //}


















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
