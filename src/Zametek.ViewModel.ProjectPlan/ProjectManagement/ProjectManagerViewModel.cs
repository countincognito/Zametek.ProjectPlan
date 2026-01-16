using Avalonia.Controls;
using Avalonia.Threading;
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
        private readonly ConcurrentDictionary<Guid, IManagedNodeViewModel> m_ManagedNodeLookup;
        private readonly ConcurrentDictionary<Guid, ProjectPlanFileModel> m_FilePlanLookup;
        private readonly ConcurrentDictionary<Guid, List<string>> m_NodeTagLookup;

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
            Root = new ManagedNodeViewModel(); // Placeholder until ResetRootNode is called.
            m_Nodes = new();
            SelectedNodes = [];
            SelectedNode = null;
            m_ManagedNodeLookup = new();
            m_FilePlanLookup = new();
            m_NodeTagLookup = new();

            SetSelectedManagedNodesCommand = ReactiveCommand.Create<SelectionChangedEventArgs>(SetSelectedManagedNodes);
            {
                ReactiveCommand<Unit, Unit> loadProjectPlanFileCommand = ReactiveCommand.CreateFromTask(
                    LoadProjectPlanFileAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null && !selectedNode.IsFolder),
                    RxApp.MainThreadScheduler);
                loadProjectPlanFileCommand.IsExecuting.ToProperty(this, pm => pm.IsLoading, out m_IsLoading);
                LoadProjectPlanFileCommand = loadProjectPlanFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> createEmptyProjectPlanFileCommand = ReactiveCommand.CreateFromTask(
                    CreateEmptyProjectPlanFileAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null),
                    RxApp.MainThreadScheduler);
                createEmptyProjectPlanFileCommand.IsExecuting.ToProperty(this, pm => pm.IsCreating, out m_IsCreating);
                CreateEmptyProjectPlanFileCommand = createEmptyProjectPlanFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> createEmptyProjectPlanFolderCommand = ReactiveCommand.CreateFromTask(
                    CreateEmptyProjectPlanFolderAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null),
                    RxApp.MainThreadScheduler);
                createEmptyProjectPlanFolderCommand.IsExecuting.ToProperty(this, pm => pm.IsCreating, out m_IsCreating);
                CreateEmptyProjectPlanFolderCommand = createEmptyProjectPlanFolderCommand;
            }
            {
                ReactiveCommand<Unit, Unit> duplicateProjectPlanFileCommand = ReactiveCommand.CreateFromTask(
                    DuplicateProjectPlanFileAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null),
                    RxApp.MainThreadScheduler);
                duplicateProjectPlanFileCommand.IsExecuting.ToProperty(this, pm => pm.IsDuplicating, out m_IsDuplicating);
                DuplicateProjectPlanFileCommand = duplicateProjectPlanFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> renameProjectPlanFileCommand = ReactiveCommand.CreateFromTask(
                    RenameProjectPlanFileAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null),
                    RxApp.MainThreadScheduler);
                renameProjectPlanFileCommand.IsExecuting.ToProperty(this, pm => pm.IsRenaming, out m_IsRenaming);
                RenameProjectPlanFileCommand = renameProjectPlanFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> moveProjectPlanFileCommand = ReactiveCommand.CreateFromTask(
                    MoveProjectPlanFileAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null),
                    RxApp.MainThreadScheduler);
                moveProjectPlanFileCommand.IsExecuting.ToProperty(this, pm => pm.IsMoving, out m_IsMoving);
                MoveProjectPlanFileCommand = moveProjectPlanFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> removeProjectPlanFileCommand = ReactiveCommand.CreateFromTask(
                    RemoveProjectPlanFileAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null),
                    RxApp.MainThreadScheduler);
                removeProjectPlanFileCommand.IsExecuting.ToProperty(this, pm => pm.IsRemoving, out m_IsRemoving);
                RemoveProjectPlanFileCommand = removeProjectPlanFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> addNodeTagCommand = ReactiveCommand.CreateFromTask(
                    AddNodeTagAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null),
                    RxApp.MainThreadScheduler);
                AddNodeTagCommand = addNodeTagCommand;
            }
            {
                ReactiveCommand<Unit, Unit> removeNodeTagCommand = ReactiveCommand.CreateFromTask(
                    RemoveNodeTagAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null),
                    RxApp.MainThreadScheduler);
                RemoveNodeTagCommand = removeNodeTagCommand;
            }

            // Create read-only view to the source list.
            m_Nodes.Connect()
                .Sort(SortExpressionComparer<IManagedNodeViewModel>.Ascending(pm => pm.CreatedOn))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(out m_ReadOnlyNodes)
                .Subscribe();

            ResetRootNode();

            m_IsProjectPlanUpdated = this
                .WhenAnyValue(pm => pm.m_CoreViewModel.IsProjectPlanUpdated)
                .ToProperty(this, pm => pm.IsProjectPlanUpdated);

            m_ProjectHasChanges = this
                .WhenAnyValue(
                    pm => pm.IsProjectUpdated,
                    pm => pm.IsProjectPlanUpdated,
                    (isProjectUpdated, isProjectPlanUpdated) => isProjectUpdated || isProjectPlanUpdated)
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
                    Root = new ManagedNodeViewModel(
                        new ProjectPlanNodeModel
                        {
                            Id = rootId,
                            IsFolder = true,
                            Name = Resource.ProjectPlan.Labels.Label_RootNode,
                            CreatedOn = DateTimeOffset.UtcNow,
                            ModifiedOn = DateTimeOffset.UtcNow,
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
                    m_Nodes.AddRange(Root.Children);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void AddPlanFiles(IEnumerable<ProjectPlanFileModel> projectPlanFileModels)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    foreach (ProjectPlanFileModel planFileModel in projectPlanFileModels)
                    {
                        m_FilePlanLookup.TryAdd(planFileModel.NodeId, planFileModel);
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void RemovePlanFiles(IEnumerable<ProjectPlanFileModel> projectPlanFileModels)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    foreach (ProjectPlanFileModel planFileModel in projectPlanFileModels)
                    {
                        m_FilePlanLookup.TryRemove(planFileModel.NodeId, out _);
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SetPlanFile(IManagedNodeViewModel managedNode)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;

                    if (managedNode.IsFolder)
                    {
                        return;
                    }

                    if (m_FilePlanLookup.TryGetValue(managedNode.Id, out ProjectPlanFileModel? projectPlanFile))
                    {
                        managedNode.ProjectPlan = projectPlanFile.Plan;
                    }
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
                        if (!m_NodeTagLookup.TryGetValue(tagModel.NodeId, out List<string>? labels))
                        {
                            labels = [];
                            m_NodeTagLookup.TryAdd(tagModel.NodeId, labels);
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

        private void RemoveTagLabels(IEnumerable<ProjectPlanTagModel> projectPlanTagModels)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    foreach (ProjectPlanTagModel tagModel in projectPlanTagModels)
                    {
                        if (m_NodeTagLookup.TryGetValue(tagModel.NodeId, out List<string>? labels))
                        {
                            labels.Remove(tagModel.Label);
                        }
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SetTagLabels(IManagedNodeViewModel managedNode)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    if (m_NodeTagLookup.TryGetValue(managedNode.Id, out List<string>? labels))
                    {
                        managedNode.SetLabels(labels);
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string SuggestNodeName(Guid parentId, string suggestedName)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;

                    int count = 0;

                    var nameHash = m_ManagedNodeLookup.Values
                        .Where(x => x.ParentId == parentId)
                        .Select(n => n.Name)
                        .Distinct()
                        .ToHashSet();

                    string newName = suggestedName;

                    while (nameHash.Contains(newName))
                    {
                        count++;
                        newName = $@"{suggestedName}-{count}";
                    }

                    return newName;
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
                    m_ManagedNodeLookup.Clear();
                    m_FilePlanLookup.Clear();
                    m_NodeTagLookup.Clear();
                    m_Nodes.Clear();
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SetSelectedManagedNodes(SelectionChangedEventArgs args)
        {
            lock (m_Lock)
            {
                if (SelectedNodes.Count == 1)
                {
                    SelectedNode = SelectedNodes.First();
                }
                else
                {
                    SelectedNode = null;
                }
            }
        }


















        private void AddManagedNodes(IEnumerable<ProjectPlanNodeModel> projectPlanNodeModels)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;

                    // First add all the plans to the lookup.
                    foreach (ProjectPlanNodeModel projectPlanNode in projectPlanNodeModels)
                    {
                        if (!m_ManagedNodeLookup.ContainsKey(projectPlanNode.Id))
                        {
                            var projectPlan = new ManagedNodeViewModel(projectPlanNode);
                            SetPlanFile(projectPlan);
                            SetTagLabels(projectPlan);
                            m_ManagedNodeLookup.TryAdd(projectPlan.Id, projectPlan);
                        }
                    }

                    // Now build the hierarchy.
                    // Remember that the Root node is not in the lookup and forms the top-level parent.
                    foreach (ProjectPlanNodeModel projectPlanNode in projectPlanNodeModels)
                    {
                        if (m_ManagedNodeLookup.TryGetValue(projectPlanNode.Id, out IManagedNodeViewModel? managedNode))
                        {
                            // Top-level node.
                            if (managedNode.ParentId == Root.Id)
                            {
                                Root.AddChildren([managedNode]);
                                m_Nodes.Add(managedNode);
                            }
                            // Child node.
                            else if (m_ManagedNodeLookup.TryGetValue(managedNode.ParentId, out IManagedNodeViewModel? parentNode))
                            {
                                parentNode.AddChildren([managedNode]);
                            }
                            else
                            // Orphaned node - treat as top-level.
                            {
                                managedNode.ParentId = Root.Id;
                                Root.AddChildren([managedNode]);
                                m_Nodes.Add(managedNode);
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

        private void MarkNodeAsLoaded(Guid nodeId)
        {
            lock (m_Lock)
            {
                // Change the loaded status in the treeview.
                IManagedNodeViewModel? currentNode = GetNode(nodeId);

                if (currentNode is not null)
                {
                    foreach (IManagedNodeViewModel node in m_ManagedNodeLookup.Values)
                    {
                        node.IsLoaded = false;
                    }
                    currentNode.IsLoaded = true;
                }
            }
        }

        private async Task LoadProjectPlanFileAsync()
        {
            try
            {
                if (IsProjectPlanUpdated)
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
                    IManagedNodeViewModel? managedNode = SelectedNode;

                    if (managedNode is not null)
                    {
                        ProjectPlanNodeModel selectedPlanNodeModel = managedNode.Node;
                        Guid nodeId = selectedPlanNodeModel.Id;

                        if (m_FilePlanLookup.TryGetValue(nodeId, out ProjectPlanFileModel? projectPlanFile))
                        {
                            ProjectPlanModel projectPlanModel = projectPlanFile.Plan;
                            m_CoreViewModel.ProcessProjectPlan(projectPlanModel, nodeId);
                            MarkNodeAsLoaded(nodeId);
                        }

                        IsProjectUpdated = false;
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
        }












        private async Task CreateEmptyProjectPlanFileAsync()
        {
            try
            {
                await CreateEmptyProjectPlanFileInternalAsync();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task CreateEmptyProjectPlanFileInternalAsync() => await Task.Run(CreateEmptyProjectPlanFileInternal);

        private void CreateEmptyProjectPlanFileInternal()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    IManagedNodeViewModel? managedNode = SelectedNode;

                    if (managedNode is not null)
                    {
                        Guid parentId = managedNode.ParentId;

                        if (managedNode.IsFolder)
                        {
                            parentId = managedNode.Id;
                        }

                        var projectPlan = m_CoreViewModel.CreateEmptyProjectPlan();
                        string nodeName = SuggestNodeName(parentId, Resource.ProjectPlan.Labels.Label_EmptyNode);

                        var projectPlanNode = new ProjectPlanNodeModel
                        {
                            Id = Guid.NewGuid(),
                            ParentId = parentId,
                            IsFolder = false,
                            Name = nodeName,
                            CreatedOn = DateTimeOffset.UtcNow,
                            ModifiedOn = DateTimeOffset.UtcNow,
                        };

                        var projectPlanFile = new ProjectPlanFileModel
                        {
                            NodeId = projectPlanNode.Id,
                            Plan = projectPlan,
                        };

                        AddPlanFiles([projectPlanFile]);
                        AddManagedNodes([projectPlanNode]);
                        IsProjectUpdated = true;
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
        }










        private async Task CreateEmptyProjectPlanFolderAsync()
        {
            try
            {
                await CreateEmptyProjectPlanFolderInternalAsync();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task CreateEmptyProjectPlanFolderInternalAsync() => await Task.Run(CreateEmptyProjectPlanFolderInternal);

        private void CreateEmptyProjectPlanFolderInternal()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    IManagedNodeViewModel? managedNode = SelectedNode;

                    if (managedNode is not null)
                    {
                        Guid parentId = managedNode.ParentId;

                        if (managedNode.IsFolder)
                        {
                            parentId = managedNode.Id;
                        }

                        string nodeName = SuggestNodeName(parentId, Resource.ProjectPlan.Labels.Label_EmptyNode);

                        var projectPlanNode = new ProjectPlanNodeModel
                        {
                            Id = Guid.NewGuid(),
                            ParentId = parentId,
                            IsFolder = true,
                            Name = nodeName,
                            CreatedOn = DateTimeOffset.UtcNow,
                            ModifiedOn = DateTimeOffset.UtcNow,
                        };

                        AddManagedNodes([projectPlanNode]);
                        IsProjectUpdated = true;
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
        }
















        private async Task DuplicateProjectPlanFileAsync()
        {
            try
            {
                if (IsProjectPlanUpdated)
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
                await DuplicateProjectPlanFileInternalAsync();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task DuplicateProjectPlanFileInternalAsync() => await Task.Run(DuplicateProjectPlanFileInternal);

        private void DuplicateProjectPlanFileInternal()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    //IManagedPlanViewModel? managedPlan = SelectedPlan;

                    //if (managedPlan is not null)
                    //{
                    //    // A branched plan shares a parent but has a new Id.
                    //    ProjectPlanNodeModel selectedPlanNodeModel = managedPlan.Node with
                    //    {
                    //        Id = Guid.NewGuid(),
                    //        CreatedOn = DateTimeOffset.UtcNow,
                    //        ModifiedOn = DateTimeOffset.UtcNow,
                    //        Comment = string.Empty,
                    //    };
                    //    Guid projectPlanId = selectedPlanNodeModel.Id;
                    //    ProjectPlanModel projectPlanModel = selectedPlanNodeModel.ProjectPlan;
                    //    AddManagedPlans([selectedPlanNodeModel]);
                    //    m_CoreViewModel.ProcessProjectPlan(projectPlanModel, projectPlanId);
                    //    MarkProjectPlanAsLoaded(projectPlanId);
                    //    IsProjectUpdated = true;
                    //}
                }
            }
            finally
            {
                IsBusy = false;
            }
        }













        private async Task RenameProjectPlanFileAsync()
        {
            try
            {
                if (IsProjectPlanUpdated)
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
                await RenameProjectPlanFileInternalAsync();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task RenameProjectPlanFileInternalAsync() => await Task.Run(RenameProjectPlanFileInternal);

        private void RenameProjectPlanFileInternal()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    //IManagedPlanViewModel? managedPlan = SelectedPlan;

                    //if (managedPlan is not null)
                    //{
                    //    // A branched plan shares a parent but has a new Id.
                    //    ProjectPlanNodeModel selectedPlanNodeModel = managedPlan.Node with
                    //    {
                    //        Id = Guid.NewGuid(),
                    //        CreatedOn = DateTimeOffset.UtcNow,
                    //        ModifiedOn = DateTimeOffset.UtcNow,
                    //        Comment = string.Empty,
                    //    };
                    //    Guid projectPlanId = selectedPlanNodeModel.Id;
                    //    ProjectPlanModel projectPlanModel = selectedPlanNodeModel.ProjectPlan;
                    //    AddManagedPlans([selectedPlanNodeModel]);
                    //    m_CoreViewModel.ProcessProjectPlan(projectPlanModel, projectPlanId);
                    //    MarkProjectPlanAsLoaded(projectPlanId);
                    //    IsProjectUpdated = true;
                    //}
                }
            }
            finally
            {
                IsBusy = false;
            }
        }












        private async Task MoveProjectPlanFileAsync()
        {
            try
            {
                if (IsProjectPlanUpdated)
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
                await MoveProjectPlanFileInternalAsync();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task MoveProjectPlanFileInternalAsync() => await Task.Run(MoveProjectPlanFileInternal);

        private void MoveProjectPlanFileInternal()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    //IManagedPlanViewModel? managedPlan = SelectedPlan;

                    //if (managedPlan is not null)
                    //{
                    //    // A branched plan shares a parent but has a new Id.
                    //    ProjectPlanNodeModel selectedPlanNodeModel = managedPlan.Node with
                    //    {
                    //        Id = Guid.NewGuid(),
                    //        CreatedOn = DateTimeOffset.UtcNow,
                    //        ModifiedOn = DateTimeOffset.UtcNow,
                    //        Comment = string.Empty,
                    //    };
                    //    Guid projectPlanId = selectedPlanNodeModel.Id;
                    //    ProjectPlanModel projectPlanModel = selectedPlanNodeModel.ProjectPlan;
                    //    AddManagedPlans([selectedPlanNodeModel]);
                    //    m_CoreViewModel.ProcessProjectPlan(projectPlanModel, projectPlanId);
                    //    MarkProjectPlanAsLoaded(projectPlanId);
                    //    IsProjectUpdated = true;
                    //}
                }
            }
            finally
            {
                IsBusy = false;
            }
        }











        private async Task RemoveProjectPlanFileAsync()
        {
            try
            {
                if (IsProjectPlanUpdated)
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
                await MoveProjectPlanFileInternalAsync();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task RemoveProjectPlanFileInternalAsync() => await Task.Run(RemoveProjectPlanFileInternal);

        private void RemoveProjectPlanFileInternal()
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    //IManagedPlanViewModel? managedPlan = SelectedPlan;

                    //if (managedPlan is not null)
                    //{
                    //    // A branched plan shares a parent but has a new Id.
                    //    ProjectPlanNodeModel selectedPlanNodeModel = managedPlan.Node with
                    //    {
                    //        Id = Guid.NewGuid(),
                    //        CreatedOn = DateTimeOffset.UtcNow,
                    //        ModifiedOn = DateTimeOffset.UtcNow,
                    //        Comment = string.Empty,
                    //    };
                    //    Guid projectPlanId = selectedPlanNodeModel.Id;
                    //    ProjectPlanModel projectPlanModel = selectedPlanNodeModel.ProjectPlan;
                    //    AddManagedPlans([selectedPlanNodeModel]);
                    //    m_CoreViewModel.ProcessProjectPlan(projectPlanModel, projectPlanId);
                    //    MarkProjectPlanAsLoaded(projectPlanId);
                    //    IsProjectUpdated = true;
                    //}
                }
            }
            finally
            {
                IsBusy = false;
            }
        }
















        private async Task AddNodeTagAsync()
        {
            try
            {
                IManagedNodeViewModel? selectedNode = SelectedNode;

                if (selectedNode is null)
                {
                    return;
                }

                var addTagViewModel = new AddNodeTagViewModel();

                bool result = await m_DialogService.ShowContextAsync(
                    title: Resource.ProjectPlan.Labels.Label_AddTag,
                    header: string.Empty,
                    message: $@"**{Resource.ProjectPlan.Messages.Message_AddTag} {selectedNode.Name}**",
                    context: addTagViewModel,
                    markdown: true);

                if (!result)
                {
                    return;
                }

                var tagModel = new ProjectPlanTagModel
                {
                    NodeId = selectedNode.Id,
                    Label = addTagViewModel.Tag,
                };

                await AddNodeTagInternalAsync(tagModel, selectedNode);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task AddNodeTagInternalAsync(ProjectPlanTagModel tagModel, IManagedNodeViewModel managedNodeViewModel) =>
            await Dispatcher.UIThread.InvokeAsync(() => AddNodeTagInternal(tagModel, managedNodeViewModel));

        private void AddNodeTagInternal(
            ProjectPlanTagModel tagModel,
            IManagedNodeViewModel managedNodeViewModel)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    AddTagLabels([tagModel]);
                    SetTagLabels(managedNodeViewModel);
                    IsProjectUpdated = true;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RemoveNodeTagAsync()
        {
            try
            {
                IManagedNodeViewModel? selectedNode = SelectedNode;

                if (selectedNode is null
                    || !m_NodeTagLookup.TryGetValue(selectedNode.Id, out List<string>? labels))
                {
                    return;
                }

                IList<ProjectPlanTagModel> tagModels = [.. labels
                    .Select(label => new ProjectPlanTagModel
                    {
                        NodeId = selectedNode.Id,
                        Label = label,
                    })];

                var removeTagViewModel = new RemoveNodeTagViewModel(tagModels);

                bool result = await m_DialogService.ShowContextAsync(
                    title: Resource.ProjectPlan.Labels.Label_DeleteTag,
                    header: string.Empty,
                    message: $@"**{Resource.ProjectPlan.Messages.Message_DeleteTag} {selectedNode.Name}**",
                    context: removeTagViewModel,
                    markdown: true);

                if (!result)
                {
                    return;
                }

                await RemoveNodeTagInternalAsync(removeTagViewModel.SelectedTag, selectedNode);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task RemoveNodeTagInternalAsync(ProjectPlanTagModel tagModel, IManagedNodeViewModel managedNodeViewModel) =>
            await Dispatcher.UIThread.InvokeAsync(() => RemoveNodeTagInternal(tagModel, managedNodeViewModel));

        private void RemoveNodeTagInternal(
            ProjectPlanTagModel tagModel,
            IManagedNodeViewModel managedNodeViewModel)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    RemoveTagLabels([tagModel]);
                    SetTagLabels(managedNodeViewModel);
                    IsProjectUpdated = true;
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

        private readonly ObservableAsPropertyHelper<bool> m_IsCreating;
        public bool IsCreating => m_IsCreating.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsDuplicating;
        public bool IsDuplicating => m_IsDuplicating.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsRenaming;
        public bool IsRenaming => m_IsRenaming.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsMoving;
        public bool IsMoving => m_IsMoving.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsRemoving;
        public bool IsRemoving => m_IsRemoving.Value;

        private bool m_IsProjectUpdated;
        public bool IsProjectUpdated
        {
            get => m_IsProjectUpdated;
            set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_IsProjectUpdated, value);
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_IsProjectPlanUpdated;
        public bool IsProjectPlanUpdated => m_IsProjectPlanUpdated.Value;

        private readonly ObservableAsPropertyHelper<bool> m_ProjectHasChanges;
        public bool ProjectHasChanges => m_ProjectHasChanges.Value;

        public IManagedNodeViewModel Root { get; private set; }

        private readonly SourceList<IManagedNodeViewModel> m_Nodes;
        private readonly ReadOnlyObservableCollection<IManagedNodeViewModel> m_ReadOnlyNodes;
        public ReadOnlyObservableCollection<IManagedNodeViewModel> Nodes => m_ReadOnlyNodes;

        public ObservableCollection<IManagedNodeViewModel> SelectedNodes { get; }

        private IManagedNodeViewModel? m_SelectedNode;
        public IManagedNodeViewModel? SelectedNode
        {
            get => m_SelectedNode;
            private set => this.RaiseAndSetIfChanged(ref m_SelectedNode, value);
        }

        public ICommand SetSelectedManagedNodesCommand { get; }

        public ICommand LoadProjectPlanFileCommand { get; }

        public ICommand CreateEmptyProjectPlanFileCommand { get; }

        public ICommand CreateEmptyProjectPlanFolderCommand { get; }

        public ICommand DuplicateProjectPlanFileCommand { get; }

        public ICommand RenameProjectPlanFileCommand { get; }

        public ICommand MoveProjectPlanFileCommand { get; }

        public ICommand RemoveProjectPlanFileCommand { get; }

        public ICommand AddNodeTagCommand { get; }

        public ICommand RemoveNodeTagCommand { get; }

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
                        IsFolder = false,
                        Name = Resource.ProjectPlan.Labels.Label_BaseNode,
                        CreatedOn = DateTimeOffset.UtcNow,
                        ModifiedOn = DateTimeOffset.UtcNow,
                    };

                    var projectPlanFile = new ProjectPlanFileModel
                    {
                        NodeId = projectPlanNode.Id,
                        Plan = projectPlan,
                    };

                    var projectPlanTag = new ProjectPlanTagModel
                    {
                        NodeId = projectPlanNode.Id,
                        Label = Resource.ProjectPlan.Labels.Label_BaseNode,
                    };

                    AddTagLabels([projectPlanTag]);
                    AddPlanFiles([projectPlanFile]);
                    AddManagedNodes([projectPlanNode]);
                    MarkNodeAsLoaded(projectPlanNode.Id);

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

                    AddPlanFiles(projectModel.Files);

                    AddManagedNodes(projectModel.Nodes);

                    // Now process the current project plan.

                    // If the list is empty, then create a new blank project plan and
                    // add it to the project.
                    if (projectModel.Nodes.Count == 0)
                    {
                        m_CoreViewModel.ResetProjectPlan();
                        var projectPlan = m_CoreViewModel.BuildProjectPlan();

                        var projectPlanNode = new ProjectPlanNodeModel
                        {
                            Id = m_CoreViewModel.ProjectPlanId,
                            ParentId = Root.Id,
                            IsFolder = false,
                            Name = Resource.ProjectPlan.Labels.Label_BaseNode,
                            CreatedOn = DateTimeOffset.UtcNow,
                            ModifiedOn = DateTimeOffset.UtcNow,
                        };

                        var projectPlanFile = new ProjectPlanFileModel
                        {
                            NodeId = projectPlanNode.Id,
                            Plan = projectPlan,
                        };

                        var projectPlanTag = new ProjectPlanTagModel
                        {
                            NodeId = projectPlanNode.Id,
                            Label = Resource.ProjectPlan.Labels.Label_BaseNode,
                        };

                        AddTagLabels([projectPlanTag]);
                        AddPlanFiles([projectPlanFile]);
                        AddManagedNodes([projectPlanNode]);
                    }
                    // Otherwise, load the project listed as current, if it exists.
                    else if (m_FilePlanLookup.TryGetValue(projectModel.Current, out ProjectPlanFileModel? projectPlanFileModel))
                    {
                        // Load the current project plan
                        Guid projectPlanId = projectPlanFileModel.NodeId;
                        ProjectPlanModel projectPlanModel = projectPlanFileModel.Plan;
                        m_CoreViewModel.ProcessProjectPlan(projectPlanModel, projectPlanId);
                        MarkNodeAsLoaded(projectPlanId);
                    }
                    // Otherwise, load the latest project plan.
                    else
                    {
                        ProjectPlanNodeModel latestPlanNodeModel = projectModel.Nodes.Last();
                        Guid latestProjectPlanId = latestPlanNodeModel.Id;

                        if (m_FilePlanLookup.TryGetValue(latestProjectPlanId, out ProjectPlanFileModel? latestProjectPlanFile))
                        {
                            ProjectPlanModel latestProjectPlanModel = latestProjectPlanFile.Plan;
                            m_CoreViewModel.ProcessProjectPlan(latestProjectPlanModel, latestProjectPlanId);
                            MarkNodeAsLoaded(latestProjectPlanId);
                        }
                    }

                    IsProjectUpdated = false;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public IManagedNodeViewModel? GetNode(Guid nodeId)
        {
            if (m_ManagedNodeLookup.TryGetValue(nodeId, out IManagedNodeViewModel? managedNode))
            {
                return managedNode;
            }
            return null;
        }

        public IManagedNodeViewModel? GetNodeParent(Guid nodeId)
        {
            if (m_ManagedNodeLookup.TryGetValue(nodeId, out IManagedNodeViewModel? managedNode)
                && m_ManagedNodeLookup.TryGetValue(managedNode.ParentId, out IManagedNodeViewModel? parentNode))
            {
                return parentNode;
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

                    Guid nodeId = m_CoreViewModel.ProjectPlanId;
                    ProjectPlanModel projectPlan = m_CoreViewModel.BuildProjectPlan();

                    IManagedNodeViewModel? managedProjectPlan = GetNode(nodeId);

                    if (managedProjectPlan is null)
                    {
                        // No existing managed plan, so add it to the Root.
                        var projectPlanNode = new ProjectPlanNodeModel
                        {
                            Id = nodeId,
                            ParentId = Root.Id,
                            IsFolder = false,
                            Name = Resource.ProjectPlan.Labels.Label_BaseNode,
                            CreatedOn = DateTimeOffset.UtcNow,
                            ModifiedOn = DateTimeOffset.UtcNow,
                        };

                        var projectPlanFile = new ProjectPlanFileModel
                        {
                            NodeId = projectPlanNode.Id,
                            Plan = projectPlan,
                        };

                        AddPlanFiles([projectPlanFile]);
                        AddManagedNodes([projectPlanNode]);
                    }
                    else
                    {
                        // Update existing managed plan.
                        managedProjectPlan.ProjectPlan = projectPlan;
                        managedProjectPlan.ModifiedOn = DateTimeOffset.UtcNow;

                        var projectPlanFile = new ProjectPlanFileModel
                        {
                            NodeId = managedProjectPlan.Id,
                            Plan = projectPlan,
                        };

                        RemovePlanFiles([projectPlanFile]);
                        AddPlanFiles([projectPlanFile]);
                    }

                    // Now build the ProjectModel.

                    Guid rootId = Root.Id;
                    Guid currentId = m_CoreViewModel.ProjectPlanId;
                    List<ProjectPlanNodeModel> nodes = [.. m_ManagedNodeLookup.Values
                        .Select(x => x.Node)
                        .OrderBy(x => x.CreatedOn)];

                    List<ProjectPlanFileModel> files = [.. m_FilePlanLookup.Values];

                    // Filter out any tags that apply to the Root node.
                    List<ProjectPlanTagModel> tags = [.. m_NodeTagLookup
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
                        Files = files,
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
                m_IsLoading?.Dispose();
                m_IsCreating?.Dispose();
                m_IsDuplicating?.Dispose();
                m_IsRenaming?.Dispose();
                m_IsMoving?.Dispose();
                m_IsRemoving?.Dispose();
                m_IsProjectPlanUpdated?.Dispose();
                m_ProjectHasChanges?.Dispose();
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
