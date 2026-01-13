using Avalonia.Controls;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System.Collections.Concurrent;
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
        private readonly ConcurrentDictionary<Guid, IManagedPlanViewModel> m_ManagedPlanLookup;
        private readonly ConcurrentDictionary<Guid, List<string>> m_PlanTagLookup;

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
            SelectedPlan = null;
            m_ManagedPlanLookup = new();
            m_PlanTagLookup = new();

            SetSelectedManagedPlansCommand = ReactiveCommand.Create<SelectionChangedEventArgs>(SetSelectedManagedPlans);
            {
                ReactiveCommand<Unit, Unit> loadProjectPlanFileCommand = ReactiveCommand.CreateFromTask(
                    LoadProjectPlanFileAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedPlan,
                        (IManagedPlanViewModel? selectedPlan) => selectedPlan is not null),
                    RxApp.MainThreadScheduler);
                loadProjectPlanFileCommand.IsExecuting.ToProperty(this, pm => pm.IsLoading, out m_IsLoading);
                LoadProjectPlanFileCommand = loadProjectPlanFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> branchProjectPlanFileCommand = ReactiveCommand.CreateFromTask(
                    BranchProjectPlanFileAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedPlan,
                        (IManagedPlanViewModel? selectedPlan) => selectedPlan is not null),
                    RxApp.MainThreadScheduler);
                branchProjectPlanFileCommand.IsExecuting.ToProperty(this, pm => pm.IsBranching, out m_IsBranching);
                BranchProjectPlanFileCommand = branchProjectPlanFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> spawnProjectPlanFileCommand = ReactiveCommand.CreateFromTask(
                    SpawnProjectPlanFileAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedPlan,
                        (IManagedPlanViewModel? selectedPlan) => selectedPlan is not null),
                    RxApp.MainThreadScheduler);
                spawnProjectPlanFileCommand.IsExecuting.ToProperty(this, pm => pm.IsSpawning, out m_IsSpawning);
                SpawnProjectPlanFileCommand = spawnProjectPlanFileCommand;
            }

            // Create read-only view to the source list.
            m_Plans.Connect()
                .Sort(SortExpressionComparer<IManagedPlanViewModel>.Ascending(pm => pm.CreatedOn))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(out m_ReadOnlyPlans)
                .Subscribe();

            ResetRootNode();

            m_ProjectHasChanges = this
                .WhenAnyValue(
                    pm => pm.m_CoreViewModel.IsProjectPlanUpdated,
                    pm => pm.IsProjectUpdated,
                    (coreIsUpdated, pmIsUpdated) => coreIsUpdated || pmIsUpdated)
                .ToProperty(this, pm => pm.ProjectHasChanges);

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
                            m_PlanTagLookup.TryAdd(tagModel.NodeId, labels);
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
                    Root.ClearChildren();
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

        private void SetSelectedManagedPlans(SelectionChangedEventArgs args)
        {
            lock (m_Lock)
            {
                if (SelectedPlans.Count == 1)
                {
                    SelectedPlan = SelectedPlans.First();
                }
                else
                {
                    SelectedPlan = null;
                }
            }
        }

        private void AddManagedPlans(IEnumerable<ProjectPlanNodeModel> projectPlanNodeModels)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;

                    // First add all the plans to the lookup.
                    foreach (ProjectPlanNodeModel projectPlanNode in projectPlanNodeModels)
                    {
                        if (!m_ManagedPlanLookup.ContainsKey(projectPlanNode.Id))
                        {
                            var projectPlan = new ManagedPlanViewModel(projectPlanNode);
                            SetTagLabels(projectPlan);
                            m_ManagedPlanLookup.TryAdd(projectPlan.Id, projectPlan);
                        }
                    }

                    // Now build the hierarchy.
                    // Remember that the Root node is not in the lookup and forms the top-level parent.
                    foreach (ProjectPlanNodeModel projectPlanNode in projectPlanNodeModels)
                    {
                        if (m_ManagedPlanLookup.TryGetValue(projectPlanNode.Id, out IManagedPlanViewModel? projectPlan))
                        {
                            // Top-level plan.
                            if (projectPlan.ParentId == Root.Id)
                            {
                                Root.AddChildren([projectPlan]);
                                m_Plans.Add(projectPlan);
                            }
                            // Child plan.
                            else if (m_ManagedPlanLookup.TryGetValue(projectPlan.ParentId, out IManagedPlanViewModel? parentPlan))
                            {
                                parentPlan.AddChildren([projectPlan]);
                            }
                            else
                            // Orphaned plan - treat as top-level.
                            {
                                projectPlan.ParentId = Root.Id;
                                Root.AddChildren([projectPlan]);
                                m_Plans.Add(projectPlan);
                            }
                        }
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadProjectPlanFileAsync()
        {
            try
            {
                if (ProjectHasChanges)
                {
                    bool confirmation = await m_DialogService.ShowConfirmationAsync(
                        Resource.ProjectPlan.Titles.Title_UnsavedChanges,
                        string.Empty,
                        Resource.ProjectPlan.Messages.Message_UnsavedChanges);

                    if (!confirmation)
                    {
                        return;
                    }
                }
                await LoadProjectPlanFileInternalAsync();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task LoadProjectPlanFileInternalAsync() => await Task.Run(LoadProjectPlanFileInternal);

        private void LoadProjectPlanFileInternal()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    IManagedPlanViewModel? managedPlan = SelectedPlan;

                    if (managedPlan is not null)
                    {
                        ProjectPlanNodeModel selectedPlanNodeModel = managedPlan.Node;
                        Guid projectPlanId = selectedPlanNodeModel.Id;
                        ProjectPlanModel projectPlanModel = selectedPlanNodeModel.ProjectPlan;
                        m_CoreViewModel.ProcessProjectPlan(projectPlanModel, projectPlanId);
                        IsProjectUpdated = false;
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task BranchProjectPlanFileAsync()
        {
            try
            {
                if (ProjectHasChanges)
                {
                    bool confirmation = await m_DialogService.ShowConfirmationAsync(
                        Resource.ProjectPlan.Titles.Title_UnsavedChanges,
                        string.Empty,
                        Resource.ProjectPlan.Messages.Message_UnsavedChanges);

                    if (!confirmation)
                    {
                        return;
                    }
                }
                await BranchProjectPlanFileInternalAsync();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task BranchProjectPlanFileInternalAsync() => await Task.Run(BranchProjectPlanFileInternal);

        private void BranchProjectPlanFileInternal()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    IManagedPlanViewModel? managedPlan = SelectedPlan;

                    if (managedPlan is not null)
                    {
                        // A branched plan shares a parent but has a new Id.
                        ProjectPlanNodeModel selectedPlanNodeModel = managedPlan.Node with
                        {
                            Id = Guid.NewGuid(),
                            CreatedOn = DateTimeOffset.UtcNow,
                            ModifiedOn = DateTimeOffset.UtcNow,
                            Comment = string.Empty,
                        };
                        Guid projectPlanId = selectedPlanNodeModel.Id;
                        ProjectPlanModel projectPlanModel = selectedPlanNodeModel.ProjectPlan;
                        AddManagedPlans([selectedPlanNodeModel]);
                        m_CoreViewModel.ProcessProjectPlan(projectPlanModel, projectPlanId);
                        IsProjectUpdated = true;
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SpawnProjectPlanFileAsync()
        {
            try
            {
                if (ProjectHasChanges)
                {
                    bool confirmation = await m_DialogService.ShowConfirmationAsync(
                        Resource.ProjectPlan.Titles.Title_UnsavedChanges,
                        string.Empty,
                        Resource.ProjectPlan.Messages.Message_UnsavedChanges);

                    if (!confirmation)
                    {
                        return;
                    }
                }
                await SpawnProjectPlanFileInternalAsync();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task SpawnProjectPlanFileInternalAsync() => await Task.Run(SpawnProjectPlanFileInternal);

        private void SpawnProjectPlanFileInternal()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    IManagedPlanViewModel? managedPlan = SelectedPlan;

                    if (managedPlan is not null)
                    {
                        // A spawned plan makes the previous plan its parent.
                        ProjectPlanNodeModel selectedPlanNodeModel = managedPlan.Node with
                        {
                            Id = Guid.NewGuid(),
                            ParentId = managedPlan.Id,
                            CreatedOn = DateTimeOffset.UtcNow,
                            ModifiedOn = DateTimeOffset.UtcNow,
                            Comment = string.Empty,
                        };
                        Guid projectPlanId = selectedPlanNodeModel.Id;
                        ProjectPlanModel projectPlanModel = selectedPlanNodeModel.ProjectPlan;
                        AddManagedPlans([selectedPlanNodeModel]);
                        m_CoreViewModel.ProcessProjectPlan(projectPlanModel, projectPlanId);
                        IsProjectUpdated = true;
                    }
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

        private bool m_IsProjectUpdated;
        public bool IsProjectUpdated
        {
            get => m_IsProjectUpdated;
            set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_IsProjectUpdated, value);
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_IsLoading;
        public bool IsLoading => m_IsLoading.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsBranching;
        public bool IsBranching => m_IsBranching.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsSpawning;
        public bool IsSpawning => m_IsSpawning.Value;

        private readonly ObservableAsPropertyHelper<bool> m_ProjectHasChanges;
        public bool ProjectHasChanges => m_ProjectHasChanges.Value;

        public IManagedPlanViewModel Root { get; private set; }

        private readonly SourceList<IManagedPlanViewModel> m_Plans;
        private readonly ReadOnlyObservableCollection<IManagedPlanViewModel> m_ReadOnlyPlans;
        public ReadOnlyObservableCollection<IManagedPlanViewModel> Plans => m_ReadOnlyPlans;

        public ObservableCollection<IManagedPlanViewModel> SelectedPlans { get; }

        private IManagedPlanViewModel? m_SelectedPlan;
        public IManagedPlanViewModel? SelectedPlan
        {
            get => m_SelectedPlan;
            private set => this.RaiseAndSetIfChanged(ref m_SelectedPlan, value);
        }

        public ICommand SetSelectedManagedPlansCommand { get; }

        public ICommand LoadProjectPlanFileCommand { get; }

        public ICommand BranchProjectPlanFileCommand { get; }

        public ICommand SpawnProjectPlanFileCommand { get; }

        public void ResetProject()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;

                    // Reset the core project plan.
                    m_CoreViewModel.ResetProjectPlan();

                    // Reset the project manager.
                    ClearProject();
                    ResetRootNode();

                    // Now add the new core project plan to the project manager.
                    ProjectPlanModel projectPlan = m_CoreViewModel.BuildProjectPlan();

                    var projectPlanNode = new ProjectPlanNodeModel
                    {
                        Id = m_CoreViewModel.ProjectPlanId,
                        ParentId = Root.Id,
                        CreatedOn = DateTimeOffset.UtcNow,
                        ModifiedOn = DateTimeOffset.UtcNow,
                        Comment = string.Empty,
                        ProjectPlan = projectPlan,
                    };

                    AddManagedPlans([projectPlanNode]);

                    m_SettingService.Reset();
                    IsProjectUpdated = false;
                }
            }
            finally
            {
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

                    // Now process the current project plan.

                    // If the list is empty, then create a new blank project plan and
                    // add it to the project.
                    if (projectModel.Nodes.Count == 0)
                    {
                        m_CoreViewModel.ResetProjectPlan();
                        var planModel = m_CoreViewModel.BuildProjectPlan();

                        var planNodeModel = new ProjectPlanNodeModel
                        {
                            Id = m_CoreViewModel.ProjectPlanId,
                            ParentId = Root.Id,
                            CreatedOn = DateTimeOffset.UtcNow,
                            ModifiedOn = DateTimeOffset.UtcNow,
                            Comment = string.Empty,
                            ProjectPlan = planModel,
                        };

                        AddManagedPlans([planNodeModel]);
                    }
                    // Otherwise, load the project listed as current, if it exists.
                    else if (m_ManagedPlanLookup.TryGetValue(projectModel.Current, out IManagedPlanViewModel? currentPlan))
                    {
                        // Load the current project plan
                        ProjectPlanNodeModel currentPlanNodeModel = currentPlan.Node;
                        Guid projectPlanId = currentPlanNodeModel.Id;
                        ProjectPlanModel projectPlanModel = currentPlanNodeModel.ProjectPlan;
                        m_CoreViewModel.ProcessProjectPlan(projectPlanModel, projectPlanId);
                    }
                    // Otherwise, load the latest project plan.
                    else
                    {
                        ProjectPlanNodeModel latestPlanNodeModel = projectModel.Nodes.Last();
                        Guid projectPlanId = latestPlanNodeModel.Id;
                        ProjectPlanModel projectPlanModel = latestPlanNodeModel.ProjectPlan;
                        m_CoreViewModel.ProcessProjectPlan(projectPlanModel, projectPlanId);
                    }

                    IsProjectUpdated = false;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public IManagedPlanViewModel? GetProjectPlan(Guid projectPlanId)
        {
            if (m_ManagedPlanLookup.TryGetValue(projectPlanId, out IManagedPlanViewModel? projectPlan))
            {
                return projectPlan;
            }
            return null;
        }

        public IManagedPlanViewModel? GetProjectPlanParent(Guid projectPlanId)
        {
            if (m_ManagedPlanLookup.TryGetValue(projectPlanId, out IManagedPlanViewModel? projectPlan)
                && m_ManagedPlanLookup.TryGetValue(projectPlan.ParentId, out IManagedPlanViewModel? parentProjectPlan))
            {
                return parentProjectPlan;
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

                    // Ensure that the current project plan is up to date.

                    Guid projectPlanId = m_CoreViewModel.ProjectPlanId;
                    ProjectPlanModel projectPlan = m_CoreViewModel.BuildProjectPlan();

                    IManagedPlanViewModel? managedProjectPlan = GetProjectPlan(projectPlanId);

                    if (managedProjectPlan is null)
                    {
                        // No existing managed plan, so add it to the Root.
                        var projectPlanNode = new ProjectPlanNodeModel
                        {
                            Id = projectPlanId,
                            ParentId = Root.Id,
                            CreatedOn = DateTimeOffset.UtcNow,
                            ModifiedOn = DateTimeOffset.UtcNow,
                            Comment = string.Empty,
                            ProjectPlan = projectPlan,
                        };
                        AddManagedPlans([projectPlanNode]);
                    }
                    else
                    {
                        // Update existing managed plan.
                        managedProjectPlan.ProjectPlan = projectPlan;
                        managedProjectPlan.ModifiedOn = DateTimeOffset.UtcNow;
                    }

                    // Now build the ProjectModel.

                    Guid rootId = Root.Id;
                    Guid currentId = m_CoreViewModel.ProjectPlanId;
                    List<ProjectPlanNodeModel> nodes = [.. m_ManagedPlanLookup.Values
                        .Select(x => x.Node)
                        .OrderBy(x => x.CreatedOn)];

                    // Filter out any tags that apply to the Root node.
                    List<ProjectPlanTagModel> tags = [.. m_PlanTagLookup
                        .Where(kvp => kvp.Key != rootId)
                        .SelectMany(kvp => kvp.Value.Select(
                            label => new ProjectPlanTagModel
                            {
                                NodeId = kvp.Key,
                                Label = label,
                            }))];

                    var projectModel = new ProjectModel
                    {
                        Version = Data.ProjectPlan.Versions.ProjectLatest,
                        Root = rootId,
                        Current = currentId,
                        Nodes = nodes,
                        Tags = tags,
                    };

                    return projectModel;
                }
            }
            finally
            {
                IsBusy = false;
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
