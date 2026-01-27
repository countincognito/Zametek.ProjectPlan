using Avalonia.Controls;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Utility;
using SortDirection = Zametek.Common.ProjectPlan.SortDirection;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ProjectPlanManagerViewModel
        : ToolViewModelBase, IProjectPlanManagerViewModel, IDisposable
    {
        #region Fields

        private readonly object m_Lock;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly ConcurrentDictionary<Guid, IManagedNodeViewModel> m_ManagedNodeLookup;
        private readonly ConcurrentDictionary<Guid, ProjectPlanFileModel> m_FilePlanLookup;
        private readonly ConcurrentDictionary<Guid, List<string>> m_NodeTagLookup;

        private readonly BehaviorSubject<IComparer<IManagedNodeViewModel>> m_NodeSortComparer;

        private readonly NodeActionModel m_NodeAction;
        private readonly Subject<bool> m_NodeActionCommandManualTrigger;

        private readonly IDisposable? m_SortUpdateSub;

        #endregion

        #region Ctors

        public ProjectPlanManagerViewModel(
            ICoreViewModel coreViewModel,
            ISettingService settingService,
            IDialogService dialogService,
            IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            m_Lock = new object();
            m_CoreViewModel = coreViewModel;
            m_SettingService = settingService;
            m_DialogService = dialogService;
            m_DateTimeCalculator = dateTimeCalculator;
            m_IsBusy = false;
            m_NodeSortComparer = new(SortExpressionComparer<IManagedNodeViewModel>.Ascending(x => x.CreatedOn));
            Root = new ManagedNodeViewModel(this, m_CoreViewModel, m_SettingService, m_NodeSortComparer); // Placeholder until ResetRootNode is called.
            m_Nodes = new();
            SelectedNodes = [];
            SelectedNode = null;
            m_ManagedNodeLookup = new();
            m_FilePlanLookup = new();
            m_NodeTagLookup = new();
            m_NodeAction = new();
            m_NodeActionCommandManualTrigger = new();
            m_IsReadyToReviseTitle = ReadyToRevise.No;

            SetSelectedManagedNodesCommand = ReactiveCommand.Create<SelectionChangedEventArgs>(SetSelectedManagedNodes);
            {
                ReactiveCommand<IManagedNodeViewModel, Unit> loadProjectPlanFileCommand = ReactiveCommand.CreateFromTask<IManagedNodeViewModel>(
                    LoadProjectPlanFileAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null && !selectedNode.IsFolder),
                    RxApp.MainThreadScheduler);
                loadProjectPlanFileCommand.IsExecuting.ToProperty(this, pm => pm.IsLoading, out m_IsLoading);
                LoadProjectPlanFileCommand = loadProjectPlanFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> loadSelectedProjectPlanFileCommand = ReactiveCommand.CreateFromTask(
                    LoadSelectedProjectPlanFileAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null && !selectedNode.IsFolder),
                    RxApp.MainThreadScheduler);
                loadSelectedProjectPlanFileCommand.IsExecuting.ToProperty(this, pm => pm.IsLoading, out m_IsLoading);
                LoadSelectedProjectPlanFileCommand = loadSelectedProjectPlanFileCommand;
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
                ReactiveCommand<Unit, Unit> renameProjectPlanNodeCommand = ReactiveCommand.CreateFromTask(
                    RenameProjectPlanNodeAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null),
                    RxApp.MainThreadScheduler);
                renameProjectPlanNodeCommand.IsExecuting.ToProperty(this, pm => pm.IsRenaming, out m_IsRenaming);
                RenameProjectPlanNodeCommand = renameProjectPlanNodeCommand;
            }
            {
                ReactiveCommand<Unit, Unit> removeProjectPlanNodeCommand = ReactiveCommand.CreateFromTask(
                    RemoveProjectPlanNodeAsync,
                    // Observe any changes in the observable collection.
                    // Note that the property has no public setters, so we 
                    // assume the collection is mutated by using the Add(), 
                    // Delete(), Clear() and other similar methods.
                    SelectedNodes
                        // Convert the collection to a stream of chunks,
                        // so we have IObservable<IChangeSet<TKey, TValue>>
                        // type also known as the DynamicData monad.
                        .ToObservableChangeSet()
                        // Each time the collection changes, we get
                        // all updated items at once.
                        .ToCollection()
                        // If the collection isn't empty, we convert
                        // that to a boolean.
                        .Select(items => items.Count != 0),
                    RxApp.MainThreadScheduler);
                removeProjectPlanNodeCommand.IsExecuting.ToProperty(this, pm => pm.IsRemoving, out m_IsRemoving);
                RemoveProjectPlanNodeCommand = removeProjectPlanNodeCommand;
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
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null && selectedNode.RawLabels.Count > 0),
                    RxApp.MainThreadScheduler);
                RemoveNodeTagCommand = removeNodeTagCommand;
            }
            {
                ReactiveCommand<Unit, Unit> cutProjectPlanNodeCommand = ReactiveCommand.CreateFromTask(
                    CutProjectPlanNodeAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null && !selectedNode.IsFolder)
                        .Merge(m_NodeActionCommandManualTrigger),
                    RxApp.MainThreadScheduler);
                CutProjectPlanNodeCommand = cutProjectPlanNodeCommand;
            }
            {
                ReactiveCommand<Unit, Unit> copyProjectPlanNodeCommand = ReactiveCommand.CreateFromTask(
                    CopyProjectPlanNodeAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null && !selectedNode.IsFolder)
                        .Merge(m_NodeActionCommandManualTrigger),
                    RxApp.MainThreadScheduler);
                CopyProjectPlanNodeCommand = copyProjectPlanNodeCommand;
            }
            {
                ReactiveCommand<Unit, Unit> pasteProjectPlanNodeCommand = ReactiveCommand.CreateFromTask(
                    PasteProjectPlanNodeAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null && m_NodeAction.NodeIds.Count != 0)
                        .Merge(m_NodeActionCommandManualTrigger),
                    RxApp.MainThreadScheduler);
                PasteProjectPlanNodeCommand = pasteProjectPlanNodeCommand;
            }

            ChangeSortModeCommand = ReactiveCommand.CreateFromTask<SortMode>(ChangeSortModeAsync);
            ChangeSortDirectionCommand = ReactiveCommand.CreateFromTask<SortDirection>(ChangeSortDirectionAsync);

            // Create read-only view to the source list.
            m_Nodes.Connect()
                .AutoRefresh(node => node.Name) // Re-evaluates when this property changes.
                .AutoRefresh(node => node.CreatedOn)
                .AutoRefresh(node => node.ModifiedOn)
                .Sort(m_NodeSortComparer) // DynamicData listens to changes in this observable.
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

            m_SortUpdateSub = this
                .WhenAnyValue(
                    pm => pm.SelectedSortMode,
                    pm => pm.SelectedSortDirection)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(async _ => await ChangeSortAsync());

            Id = Resource.ProjectPlan.Titles.Title_ProjectPlans;
            Title = Resource.ProjectPlan.Titles.Title_ProjectPlans;
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

                    DateTimeOffset localNow = m_DateTimeCalculator.GetLocalNow();

                    // Root node.
                    Root.Dispose();
                    Root = new ManagedNodeViewModel(
                        this,
                        m_CoreViewModel,
                        m_SettingService,
                        m_NodeSortComparer,
                        new ProjectPlanNodeModel
                        {
                            Id = rootId,
                            IsFolder = true,
                            Name = Resource.ProjectPlan.Labels.Label_RootNode,
                            CreatedOn = localNow,
                            ModifiedOn = localNow,
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
                    m_Nodes.AddRange(Root.RawChildren);
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
                        m_FilePlanLookup[planFileModel.NodeId] = planFileModel;
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void RemovePlanFiles(IEnumerable<Guid> projectPlanFileIds)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    foreach (Guid projectPlanFileId in projectPlanFileIds)
                    {
                        m_FilePlanLookup.TryRemove(projectPlanFileId, out _);
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

        private HashSet<IManagedNodeViewModel> FindNestedNodes(Guid nodeId)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    var result = new HashSet<IManagedNodeViewModel>();

                    if (m_ManagedNodeLookup.TryGetValue(nodeId, out IManagedNodeViewModel? managedNode))
                    {
                        result.Add(managedNode);
                        foreach (IManagedNodeViewModel childNode in managedNode.RawChildren)
                        {
                            foreach (IManagedNodeViewModel nestedChild in FindNestedNodes(childNode.Id))
                            {
                                result.Add(nestedChild);
                            }
                        }
                    }

                    return result;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private HashSet<IManagedNodeViewModel> FindNestedNodes(IEnumerable<Guid> nodeIds)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    Dictionary<Guid, IManagedNodeViewModel> foundNodes = [];

                    foreach (Guid nodeId in nodeIds)
                    {
                        HashSet<IManagedNodeViewModel> nestedNodes = [];

                        if (!foundNodes.TryGetValue(nodeId, out IManagedNodeViewModel? existingNode))
                        {
                            nestedNodes = FindNestedNodes(nodeId);
                        }

                        foreach (IManagedNodeViewModel nestedNode in nestedNodes)
                        {
                            if (!foundNodes.ContainsKey(nestedNode.Id))
                            {
                                foundNodes[nestedNode.Id] = nestedNode;
                            }
                        }
                    }
                    return [.. foundNodes.Values];
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
                            m_NodeTagLookup[tagModel.NodeId] = labels;
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

        private void ClearTagLabels(IEnumerable<ProjectPlanTagModel> projectPlanTagModels)
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

        private void RemoveTagLabels(IEnumerable<Guid> projectPlanTagIds)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    foreach (Guid projectPlanTagId in projectPlanTagIds)
                    {
                        m_NodeTagLookup.TryRemove(projectPlanTagId, out _);
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

        private HashSet<string> ExistingTagNames(Guid nodeId)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    if (m_NodeTagLookup.TryGetValue(nodeId, out List<string>? labels))
                    {
                        return [.. labels];
                    }
                    return [];
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private HashSet<string> ExistingNodeNames(Guid parentId)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    HashSet<string> nameHash = [.. m_ManagedNodeLookup.Values
                        .Where(x => x.ParentId == parentId)
                        .Select(n => n.Name)
                        .Distinct()];
                    return nameHash;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static string SuggestNodeName(
            string suggestedName,
            HashSet<string> existingNames)
        {
            int count = 0;
            string newName = suggestedName;

            while (existingNames.Contains(newName))
            {
                count++;
                newName = $@"{suggestedName}-{count}";
            }

            return newName;
        }

        private string SuggestNodeName(
            Guid parentId,
            string suggestedName)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    HashSet<string> nameHash = ExistingNodeNames(parentId);
                    return SuggestNodeName(suggestedName, nameHash);
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

        private void AddManagedNodes(IEnumerable<ProjectPlanNodeModel> managedNodes)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;

                    // First add all the plans to the lookup.
                    foreach (ProjectPlanNodeModel projectPlanNode in managedNodes)
                    {
                        if (!m_ManagedNodeLookup.ContainsKey(projectPlanNode.Id))
                        {
                            var projectPlan = new ManagedNodeViewModel(this, m_CoreViewModel, m_SettingService, m_NodeSortComparer, projectPlanNode);
                            SetPlanFile(projectPlan);
                            SetTagLabels(projectPlan);
                            m_ManagedNodeLookup[projectPlan.Id] = projectPlan;
                        }
                    }

                    // Now build the hierarchy.
                    // Remember that the Root node is not in the lookup and forms the top-level parent.
                    foreach (ProjectPlanNodeModel projectPlanNode in managedNodes)
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

        private void RemoveManagedNodes(IEnumerable<IManagedNodeViewModel> managedNodes)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;

                    // Reverse the order of AddManagedNodes.

                    foreach (IManagedNodeViewModel managedNode in managedNodes)
                    {
                        if (m_ManagedNodeLookup.TryGetValue(managedNode.Id, out _))
                        {
                            // Remove from the parent.
                            if (m_ManagedNodeLookup.TryGetValue(managedNode.ParentId, out IManagedNodeViewModel? parentNode))
                            {
                                parentNode.RemoveChildren([managedNode.Id]);
                            }
                            else if (managedNode.ParentId == Root.Id)
                            {
                                Root.RemoveChildren([managedNode.Id]);
                            }
                            m_Nodes.Remove(managedNode);
                            m_ManagedNodeLookup.TryRemove(managedNode.Id, out _);
                            managedNode.Dispose();
                            m_NodeAction.NodeIds.Remove(managedNode.Id);
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
                    m_SettingService.SetProjectPlanTitle(currentNode.Name);
                }
            }
        }

        private async Task LoadSelectedProjectPlanFileAsync()
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
                IManagedNodeViewModel? managedNode = SelectedNode;
                await LoadProjectPlanFileInternalAsync(managedNode);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task LoadProjectPlanFileAsync(IManagedNodeViewModel? managedNodeViewModel)
        {
            try
            {
                if (managedNodeViewModel is null)
                {
                    return;
                }
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
                await LoadProjectPlanFileInternalAsync(managedNodeViewModel);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task LoadProjectPlanFileInternalAsync(IManagedNodeViewModel? managedNodeViewModel) =>
            await Task.Run(() => LoadProjectPlanFileInternal(managedNodeViewModel));

        private void LoadProjectPlanFileInternal(IManagedNodeViewModel? managedNodeViewModel)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;

                    if (managedNodeViewModel is null)
                    {
                        return;
                    }
                    if (managedNodeViewModel is not null)
                    {
                        ProjectPlanNodeModel selectedPlanNodeModel = managedNodeViewModel.Node;
                        Guid nodeId = selectedPlanNodeModel.Id;
                        string nodeName = selectedPlanNodeModel.Name;

                        if (m_FilePlanLookup.TryGetValue(nodeId, out ProjectPlanFileModel? projectPlanFile))
                        {
                            ProjectPlanModel projectPlanModel = projectPlanFile.Plan;
                            m_CoreViewModel.ProcessProjectPlan(projectPlanModel, nodeId, nodeName);
                            MarkNodeAsLoaded(nodeId);
                        }

                        IsProjectUpdated = true;
                        IsReadyToReviseTitle = ReadyToRevise.Yes;
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
                IManagedNodeViewModel? managedNode = SelectedNode;

                if (managedNode is null)
                {
                    return;
                }

                Guid parentId = managedNode.ParentId;

                if (managedNode.IsFolder)
                {
                    parentId = managedNode.Id;
                }

                HashSet<string> existingNames = ExistingNodeNames(parentId);

                var nodeNameViewModel = new NodeNameViewModel(
                    Resource.ProjectPlan.Labels.Label_EmptyNode,
                    existingNames,
                    SuggestNodeName);

                bool result = await m_DialogService.ShowContextAsync(
                    title: Resource.ProjectPlan.Labels.Label_NewPlan,
                    header: string.Empty,
                    message: $@"**{Resource.ProjectPlan.Labels.Label_NewPlan}**",
                    context: nodeNameViewModel,
                    markdown: true);

                nodeNameViewModel.RunValidation();

                if (!result
                    || nodeNameViewModel.HasErrors)
                {
                    return;
                }

                await CreateEmptyProjectPlanFileInternalAsync(parentId, nodeNameViewModel.Name);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task CreateEmptyProjectPlanFileInternalAsync(Guid parentId, string nodeName) =>
            await Task.Run(() => CreateEmptyProjectPlanFileInternal(parentId, nodeName));

        private void CreateEmptyProjectPlanFileInternal(
            Guid parentId,
            string nodeName)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    ProjectPlanModel projectPlan = m_CoreViewModel.CreateEmptyProjectPlan();
                    DateTimeOffset localNow = m_DateTimeCalculator.GetLocalNow();

                    var projectPlanNode = new ProjectPlanNodeModel
                    {
                        Id = Guid.NewGuid(),
                        ParentId = parentId,
                        IsFolder = false,
                        Name = nodeName,
                        CreatedOn = localNow,
                        ModifiedOn = localNow,
                    };

                    var projectPlanFile = new ProjectPlanFileModel
                    {
                        NodeId = projectPlanNode.Id,
                        Plan = projectPlan,
                    };

                    AddPlanFiles([projectPlanFile]);
                    AddManagedNodes([projectPlanNode]);
                    IsProjectUpdated = true;
                    IsReadyToReviseTitle = ReadyToRevise.Yes;
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
                IManagedNodeViewModel? managedNode = SelectedNode;

                if (managedNode is null)
                {
                    return;
                }

                Guid parentId = managedNode.ParentId;

                if (managedNode.IsFolder)
                {
                    parentId = managedNode.Id;
                }

                HashSet<string> existingNames = ExistingNodeNames(parentId);

                var nodeNameViewModel = new NodeNameViewModel(
                    Resource.ProjectPlan.Labels.Label_EmptyNode,
                    existingNames,
                    SuggestNodeName);

                bool result = await m_DialogService.ShowContextAsync(
                    title: Resource.ProjectPlan.Labels.Label_NewPlanFolder,
                    header: string.Empty,
                    message: $@"**{Resource.ProjectPlan.Labels.Label_NewPlanFolder}**",
                    context: nodeNameViewModel,
                    markdown: true);

                nodeNameViewModel.RunValidation();

                if (!result
                    || nodeNameViewModel.HasErrors)
                {
                    return;
                }

                await CreateEmptyProjectPlanFolderInternalAsync(parentId, nodeNameViewModel.Name);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task CreateEmptyProjectPlanFolderInternalAsync(Guid parentId, string nodeName) =>
            await Task.Run(() => CreateEmptyProjectPlanFolderInternal(parentId, nodeName));

        private void CreateEmptyProjectPlanFolderInternal(Guid parentId, string nodeName)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    DateTimeOffset localNow = m_DateTimeCalculator.GetLocalNow();

                    var projectPlanNode = new ProjectPlanNodeModel
                    {
                        Id = Guid.NewGuid(),
                        ParentId = parentId,
                        IsFolder = true,
                        Name = nodeName,
                        CreatedOn = localNow,
                        ModifiedOn = localNow,
                    };

                    AddManagedNodes([projectPlanNode]);
                    IsProjectUpdated = true;
                    IsReadyToReviseTitle = ReadyToRevise.Yes;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RenameProjectPlanNodeAsync()
        {
            try
            {
                IManagedNodeViewModel? managedNode = SelectedNode;

                if (managedNode is null)
                {
                    return;
                }

                Guid parentId = managedNode.ParentId;
                string currentName = managedNode.Name;
                HashSet<string> existingNames = ExistingNodeNames(parentId);
                existingNames.Remove(currentName);

                var nodeNameViewModel = new NodeNameViewModel(
                    currentName,
                    existingNames,
                    SuggestNodeName);

                bool result = await m_DialogService.ShowContextAsync(
                    title: Resource.ProjectPlan.Labels.Label_NewName,
                    header: string.Empty,
                    message: $@"**{Resource.ProjectPlan.Labels.Label_NewName}**",
                    context: nodeNameViewModel,
                    markdown: true);

                nodeNameViewModel.RunValidation();

                if (!result
                    || nodeNameViewModel.HasErrors)
                {
                    return;
                }

                managedNode.Name = nodeNameViewModel.Name;

                // update the plan title setting if the renamed node is the currently loaded plan.
                if (managedNode.Id == m_SettingService.ProjectPlanId)
                {
                    m_SettingService.SetProjectPlanTitle(managedNode.Name);
                }

                IsProjectUpdated = true;
                IsReadyToReviseTitle = ReadyToRevise.Yes;
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task RemoveProjectPlanNodeAsync()
        {
            try
            {
                IList<IManagedNodeViewModel> managedNodes = SelectedNodes.AsReadOnly();

                if (managedNodes.Count == 0)
                {
                    return;
                }

                bool confirmation = await m_DialogService.ShowConfirmationAsync(
                    Resource.ProjectPlan.Titles.Title_DeleteNodes,
                    string.Empty,
                    string.Format(Resource.ProjectPlan.Messages.Message_DoYouWishToDeleteTheseItems));

                if (!confirmation)
                {
                    return;
                }

                await RemoveProjectPlanNodeInternalAsync(managedNodes.Select(x => x.Id));
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task RemoveProjectPlanNodeInternalAsync(IEnumerable<Guid> nodeIds) =>
            await Task.Run(() => RemoveProjectPlanNodeInternal(nodeIds));

        private void RemoveProjectPlanNodeInternal(IEnumerable<Guid> nodeIds)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    HashSet<IManagedNodeViewModel> nestedNodes = FindNestedNodes(nodeIds);

                    RemoveManagedNodes(nestedNodes);
                    RemovePlanFiles([.. nestedNodes.Where(x => !x.IsFolder).Select(x => x.Id)]);
                    RemoveTagLabels([.. nestedNodes.Select(x => x.Id)]);

                    // If the managed plan being removed is the currently loaded plan, then
                    // reset the core project plan to the most recently added plan, if any.
                    // If there are no remaining plans, then create a new blank project plan.

                    Guid currentNodeId = m_SettingService.ProjectPlanId;
                    HashSet<Guid> nestedNodeIds = [.. nestedNodes.Select(n => n.Id).Distinct()];

                    if (!nestedNodeIds.Contains(currentNodeId))
                    {
                        IsProjectUpdated = true;
                        IsReadyToReviseTitle = ReadyToRevise.Yes;
                        return;
                    }

                    // Find the most recently modified plan that is not being removed.
                    IManagedNodeViewModel? mostRecentPlan = m_ManagedNodeLookup.Values
                        .Where(x => !x.IsFolder)
                        .OrderByDescending(x => x.Node.ModifiedOn)
                        .FirstOrDefault();

                    // If found, load it.
                    if (mostRecentPlan is not null)
                    {
                        LoadProjectPlanFileInternal(mostRecentPlan);
                        IsProjectUpdated = true;
                        IsReadyToReviseTitle = ReadyToRevise.Yes;
                        return;
                    }

                    // Otherwise, we need to create a new blank project plan.

                    // Reset the core project plan.
                    m_CoreViewModel.ResetProjectPlan();

                    // Now add the new core project plan to the project manager.
                    ProjectPlanModel projectPlan = m_CoreViewModel.BuildProjectPlan();
                    DateTimeOffset localNow = m_DateTimeCalculator.GetLocalNow();

                    var projectPlanNode = new ProjectPlanNodeModel
                    {
                        Id = m_SettingService.ProjectPlanId,
                        ParentId = Root.Id,
                        IsFolder = false,
                        Name = Resource.ProjectPlan.Labels.Label_BaseNode,
                        CreatedOn = localNow,
                        ModifiedOn = localNow,
                    };

                    var projectPlanFile = new ProjectPlanFileModel
                    {
                        NodeId = projectPlanNode.Id,
                        Plan = projectPlan,
                    };

                    AddTagLabels([]); // Don't really need this, but for consistency.
                    AddPlanFiles([projectPlanFile]);
                    AddManagedNodes([projectPlanNode]);

                    MarkNodeAsLoaded(projectPlanNode.Id);

                    IsProjectUpdated = true;
                    IsReadyToReviseTitle = ReadyToRevise.Yes;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task InvokeNodeActionChecksAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                m_NodeActionCommandManualTrigger.OnNext(true);
            });
        }

        private async Task CutProjectPlanNodeAsync()
        {
            try
            {
                IManagedNodeViewModel? managedNode = SelectedNode;

                if (managedNode is null
                    || managedNode.IsFolder)
                {
                    return;
                }

                lock (m_Lock)
                {
                    m_NodeAction.SetCut([managedNode.Id]);
                }
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
            finally
            {
                await InvokeNodeActionChecksAsync();
            }
        }

        private async Task CopyProjectPlanNodeAsync()
        {
            try
            {
                IManagedNodeViewModel? managedNode = SelectedNode;

                if (managedNode is null
                    || managedNode.IsFolder)
                {
                    return;
                }

                lock (m_Lock)
                {
                    m_NodeAction.SetCopy([managedNode.Id]);
                }
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
            finally
            {
                await InvokeNodeActionChecksAsync();
            }
        }

        private async Task PasteProjectPlanNodeAsync()
        {
            try
            {
                IManagedNodeViewModel? managedNode = SelectedNode;

                if (managedNode is null)
                {
                    return;
                }

                Guid destinationParentId = managedNode.ParentId;

                if (managedNode.IsFolder)
                {
                    destinationParentId = managedNode.Id;
                }

                await PasteProjectPlanNodeInternalAsync(destinationParentId);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
            finally
            {
                await InvokeNodeActionChecksAsync();
            }
        }

        private async Task PasteProjectPlanNodeInternalAsync(Guid destinationParentId) =>
            await Task.Run(() => PasteProjectPlanNodeInternal(destinationParentId));

        private void PasteProjectPlanNodeInternal(Guid destinationParentId)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    DateTimeOffset localNow = m_DateTimeCalculator.GetLocalNow();

                    foreach (Guid nodeId in m_NodeAction.NodeIds)
                    {
                        if (m_ManagedNodeLookup.TryGetValue(nodeId, out IManagedNodeViewModel? managedNode)
                            && !managedNode.IsFolder
                            && managedNode.ProjectPlan is not null)
                        {
                            var projectPlanNode = new ProjectPlanNodeModel
                            {
                                Id = Guid.NewGuid(),
                                ParentId = destinationParentId,
                                IsFolder = false,
                                Name = managedNode.Name,
                                CreatedOn = localNow,
                                ModifiedOn = localNow,
                            };

                            var projectPlanFile = new ProjectPlanFileModel
                            {
                                NodeId = projectPlanNode.Id,
                                Plan = managedNode.ProjectPlan.CloneObject(),
                            };

                            AddTagLabels([]); // Don't really need this, but for consistency.
                            AddPlanFiles([projectPlanFile]);
                            AddManagedNodes([projectPlanNode]);

                            // Post paste action.
                            NodeAction action = m_NodeAction.Action;
                            switch (action)
                            {
                                case NodeAction.Cut:
                                    RemoveProjectPlanNodeInternal([nodeId]);
                                    break;
                                case NodeAction.Copy:
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException(nameof(action));
                            }

                            IsProjectUpdated = true;
                            IsReadyToReviseTitle = ReadyToRevise.Yes;
                        }
                    }
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
                IManagedNodeViewModel? managedNode = SelectedNode;

                if (managedNode is null)
                {
                    return;
                }

                HashSet<string> existingTagNames = ExistingTagNames(managedNode.Id);

                var addTagViewModel = new AddNodeTagViewModel(existingTagNames);

                bool result = await m_DialogService.ShowContextAsync(
                    title: Resource.ProjectPlan.Labels.Label_AddTag,
                    header: string.Empty,
                    message: $@"**{Resource.ProjectPlan.Messages.Message_AddTag} {managedNode.Name}**",
                    context: addTagViewModel,
                    markdown: true);

                addTagViewModel.RunValidation();

                if (!result
                    || addTagViewModel.HasErrors)
                {
                    return;
                }

                var tagModel = new ProjectPlanTagModel
                {
                    NodeId = managedNode.Id,
                    Label = addTagViewModel.Tag,
                };

                await AddNodeTagInternalAsync(tagModel, managedNode);
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
                    //MarkNodeAsLoaded(tagModel.NodeId);
                    IsProjectUpdated = true;
                    IsReadyToReviseTitle = ReadyToRevise.Yes;
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
                IManagedNodeViewModel? managedNode = SelectedNode;

                if (managedNode is null
                    || !m_NodeTagLookup.TryGetValue(managedNode.Id, out List<string>? labels)
                    || managedNode.RawLabels.Count == 0)
                {
                    return;
                }

                IList<ProjectPlanTagModel> tagModels = [.. labels
                    .Select(label => new ProjectPlanTagModel
                    {
                        NodeId = managedNode.Id,
                        Label = label,
                    })];

                var removeTagViewModel = new RemoveNodeTagViewModel(tagModels);

                bool result = await m_DialogService.ShowContextAsync(
                    title: Resource.ProjectPlan.Labels.Label_DeleteTag,
                    header: string.Empty,
                    message: $@"**{Resource.ProjectPlan.Messages.Message_DeleteTag} {managedNode.Name}**",
                    context: removeTagViewModel,
                    markdown: true);

                if (!result)
                {
                    return;
                }

                await RemoveNodeTagInternalAsync(removeTagViewModel.SelectedTag, managedNode);
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
                    ClearTagLabels([tagModel]);
                    SetTagLabels(managedNodeViewModel);
                    //MarkNodeAsLoaded(tagModel.NodeId);
                    IsProjectUpdated = true;
                    IsReadyToReviseTitle = ReadyToRevise.Yes;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ChangeSortModeAsync(SortMode sortMode)
        {
            try
            {
                SelectedSortMode = sortMode;
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task ChangeSortDirectionAsync(SortDirection sortDirection)
        {
            try
            {
                SelectedSortDirection = sortDirection;
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task ChangeSortAsync()
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(ChangeSortInternal);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private void ChangeSortInternal()
        {
            lock (m_Lock)
            {
                SortMode sortMode = SelectedSortMode;
                SortDirection sortDirection = SelectedSortDirection;

                Func<IManagedNodeViewModel, IComparable> newSortMode =
                    (x) => x.Name;
                Func<Func<IManagedNodeViewModel, IComparable>, SortExpressionComparer<IManagedNodeViewModel>> newSortDirection =
                    SortExpressionComparer<IManagedNodeViewModel>.Ascending;

                newSortMode = sortMode switch
                {
                    SortMode.Name => (x) => x.Name,
                    SortMode.CreatedOn => (x) => x.CreatedOn,
                    SortMode.ModifiedOn => (x) => x.ModifiedOn,
                    _ => throw new ArgumentOutOfRangeException(nameof(sortMode), @$"{Resource.ProjectPlan.Messages.Message_UnknownSortMode} {sortMode}"),
                };

                newSortDirection = sortDirection switch
                {
                    SortDirection.Ascending => SortExpressionComparer<IManagedNodeViewModel>.Ascending,
                    SortDirection.Descending => SortExpressionComparer<IManagedNodeViewModel>.Descending,
                    _ => throw new ArgumentOutOfRangeException(nameof(sortMode), @$"{Resource.ProjectPlan.Messages.Message_UnknownSortDirection} {sortDirection}"),
                };

                IComparer<IManagedNodeViewModel> nodeSortComparer = newSortDirection(newSortMode);
                m_NodeSortComparer.OnNext(nodeSortComparer);
            }
        }

        #endregion

        #region IProjectPlanManagerViewModel

        private bool m_IsBusy;
        public bool IsBusy
        {
            get => m_IsBusy;
            private set
            {
                this.RaiseAndSetIfChanged(ref m_IsBusy, value);
            }
        }

        // We need to use an enum because raised changes on bools aren't always captured.
        // https://github.com/reactiveui/ReactiveUI/issues/3846
        private ReadyToRevise m_IsReadyToReviseTitle;

        // This should always be the last thing altered in order to trigger a check.
        public ReadyToRevise IsReadyToReviseTitle
        {
            get => m_IsReadyToReviseTitle;
            set
            {
                m_IsReadyToReviseTitle = value;
                this.RaisePropertyChanged();
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_IsLoading;
        public bool IsLoading => m_IsLoading.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsCreating;
        public bool IsCreating => m_IsCreating.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsRenaming;
        public bool IsRenaming => m_IsRenaming.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsRemoving;
        public bool IsRemoving => m_IsRemoving.Value;

        private bool m_IsProjectUpdated;
        public bool IsProjectUpdated
        {
            get => m_IsProjectUpdated;
            set
            {
                //lock (m_Lock) 
                this.RaiseAndSetIfChanged(ref m_IsProjectUpdated, value);
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_IsProjectPlanUpdated;
        public bool IsProjectPlanUpdated => m_IsProjectPlanUpdated.Value;

        private readonly ObservableAsPropertyHelper<bool> m_ProjectHasChanges;
        public bool ProjectHasChanges => m_ProjectHasChanges.Value;

        public IManagedNodeViewModel Root { get; private set; }

        private readonly SourceList<IManagedNodeViewModel> m_Nodes;
        public IReadOnlyList<IManagedNodeViewModel> RawNodes => m_Nodes.Items;

        private readonly ReadOnlyObservableCollection<IManagedNodeViewModel> m_ReadOnlyNodes;
        public ReadOnlyObservableCollection<IManagedNodeViewModel> Nodes => m_ReadOnlyNodes;

        public ObservableCollection<IManagedNodeViewModel> SelectedNodes { get; }

        private IManagedNodeViewModel? m_SelectedNode;
        public IManagedNodeViewModel? SelectedNode
        {
            get => m_SelectedNode;
            private set => this.RaiseAndSetIfChanged(ref m_SelectedNode, value);
        }

        public SortMode SelectedSortMode
        {
            get => m_SettingService.ProjectPlanSortMode;
            set
            {
                lock (m_Lock)
                {
                    m_SettingService.ProjectPlanSortMode = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public SortDirection SelectedSortDirection
        {
            get => m_SettingService.ProjectPlanSortDirection;
            set
            {
                lock (m_Lock)
                {
                    m_SettingService.ProjectPlanSortDirection = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public ICommand SetSelectedManagedNodesCommand { get; }

        public ICommand LoadProjectPlanFileCommand { get; }

        public ICommand LoadSelectedProjectPlanFileCommand { get; }

        public ICommand CreateEmptyProjectPlanFileCommand { get; }

        public ICommand CreateEmptyProjectPlanFolderCommand { get; }

        public ICommand RenameProjectPlanNodeCommand { get; }

        public ICommand RemoveProjectPlanNodeCommand { get; }

        public ICommand CutProjectPlanNodeCommand { get; }

        public ICommand CopyProjectPlanNodeCommand { get; }

        public ICommand PasteProjectPlanNodeCommand { get; }

        public ICommand AddNodeTagCommand { get; }

        public ICommand RemoveNodeTagCommand { get; }

        public ICommand ChangeSortModeCommand { get; }

        public ICommand ChangeSortDirectionCommand { get; }

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

                    m_SettingService.ResetProject();

                    // Now add the new core project plan to the project manager.
                    ProjectPlanModel projectPlan = m_CoreViewModel.BuildProjectPlan();
                    DateTimeOffset localNow = m_DateTimeCalculator.GetLocalNow();

                    var projectPlanNode = new ProjectPlanNodeModel
                    {
                        Id = m_SettingService.ProjectPlanId,
                        ParentId = Root.Id,
                        IsFolder = false,
                        Name = Resource.ProjectPlan.Labels.Label_BaseNode,
                        CreatedOn = localNow,
                        ModifiedOn = localNow,
                    };

                    var projectPlanFile = new ProjectPlanFileModel
                    {
                        NodeId = projectPlanNode.Id,
                        Plan = projectPlan,
                    };

                    AddTagLabels([]); // Don't really need this, but for consistency.
                    AddPlanFiles([projectPlanFile]);
                    AddManagedNodes([projectPlanNode]);

                    MarkNodeAsLoaded(projectPlanNode.Id);

                    m_NodeAction.Reset();

                    IsProjectUpdated = false;
                    IsReadyToReviseTitle = ReadyToRevise.Yes;
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

                    m_SettingService.SetProjectId(projectModel.Id);

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
                        ProjectPlanModel projectPlan = m_CoreViewModel.BuildProjectPlan();
                        DateTimeOffset localNow = m_DateTimeCalculator.GetLocalNow();

                        var projectPlanNode = new ProjectPlanNodeModel
                        {
                            Id = m_SettingService.ProjectPlanId,
                            ParentId = Root.Id,
                            IsFolder = false,
                            Name = Resource.ProjectPlan.Labels.Label_BaseNode,
                            CreatedOn = localNow,
                            ModifiedOn = localNow,
                        };

                        var projectPlanFile = new ProjectPlanFileModel
                        {
                            NodeId = projectPlanNode.Id,
                            Plan = projectPlan,
                        };

                        AddTagLabels([]); // Don't really need this, but for consistency.
                        AddPlanFiles([projectPlanFile]);
                        AddManagedNodes([projectPlanNode]);
                    }
                    // Otherwise, load the project listed as current, if it exists.
                    else if (m_FilePlanLookup.TryGetValue(projectModel.Current, out ProjectPlanFileModel? projectPlanFileModel))
                    {
                        // Load the current project plan
                        Guid projectPlanId = projectPlanFileModel.NodeId;
                        ProjectPlanModel projectPlanModel = projectPlanFileModel.Plan;
                        IManagedNodeViewModel? projectPlanNode = GetNode(projectPlanId);
                        string projectPlanName = projectPlanNode?.Name ?? Resource.ProjectPlan.Labels.Label_UnknownNode;

                        m_CoreViewModel.ProcessProjectPlan(projectPlanModel, projectPlanId, projectPlanName);
                        MarkNodeAsLoaded(projectPlanId);
                    }
                    // Otherwise, load the latest project plan.
                    else
                    {
                        ProjectPlanNodeModel latestPlanNodeModel = projectModel.Nodes.Last();
                        Guid latestProjectPlanId = latestPlanNodeModel.Id;
                        string latestProjectPlanName = latestPlanNodeModel.Name;

                        if (m_FilePlanLookup.TryGetValue(latestProjectPlanId, out ProjectPlanFileModel? latestProjectPlanFile))
                        {
                            ProjectPlanModel latestProjectPlanModel = latestProjectPlanFile.Plan;
                            m_CoreViewModel.ProcessProjectPlan(latestProjectPlanModel, latestProjectPlanId, latestProjectPlanName);
                            MarkNodeAsLoaded(latestProjectPlanId);
                        }
                    }

                    IsProjectUpdated = false;
                    IsReadyToReviseTitle = ReadyToRevise.Yes;
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

                    Guid nodeId = m_SettingService.ProjectPlanId;
                    ProjectPlanModel projectPlan = m_CoreViewModel.BuildProjectPlan();

                    IManagedNodeViewModel? managedProjectPlan = GetNode(nodeId);
                    DateTimeOffset localNow = m_DateTimeCalculator.GetLocalNow();

                    if (managedProjectPlan is null)
                    {
                        // No existing managed plan, so add it to the Root.

                        var projectPlanNode = new ProjectPlanNodeModel
                        {
                            Id = nodeId,
                            ParentId = Root.Id,
                            IsFolder = false,
                            Name = Resource.ProjectPlan.Labels.Label_BaseNode,
                            CreatedOn = localNow,
                            ModifiedOn = localNow,
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
                        managedProjectPlan.ModifiedOn = localNow;

                        var projectPlanFile = new ProjectPlanFileModel
                        {
                            NodeId = managedProjectPlan.Id,
                            Plan = projectPlan,
                        };

                        RemovePlanFiles([projectPlanFile.NodeId]);
                        AddPlanFiles([projectPlanFile]);
                    }

                    // Now build the ProjectModel.

                    Guid projectId = m_SettingService.ProjectId;
                    Guid rootId = Root.Id;
                    Guid currentId = m_SettingService.ProjectPlanId;
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
                        Id = projectId,
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
            m_SortUpdateSub?.Dispose();
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
                m_IsRenaming?.Dispose();
                m_IsRemoving?.Dispose();
                m_IsProjectPlanUpdated?.Dispose();
                m_ProjectHasChanges?.Dispose();
                m_NodeActionCommandManualTrigger?.Dispose();
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
