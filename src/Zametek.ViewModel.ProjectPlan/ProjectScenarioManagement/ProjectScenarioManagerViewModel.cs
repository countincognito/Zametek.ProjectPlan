using Avalonia.Controls;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
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
    public class ProjectScenarioManagerViewModel
        : ToolViewModelBase, IProjectScenarioManagerViewModel, IDisposable
    {
        #region Fields

        private readonly object m_Lock;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly ConcurrentDictionary<Guid, IManagedNodeViewModel> m_ManagedNodeLookup;
        private readonly ConcurrentDictionary<Guid, ProjectScenarioFileModel> m_FileScenarioLookup;
        private readonly ConcurrentDictionary<Guid, List<string>> m_NodeTagLookup;

        private readonly BehaviorSubject<IComparer<IManagedNodeViewModel>> m_NodeSortComparer;

        private readonly NodeActionModel m_NodeAction;
        private readonly Subject<bool> m_NodeActionCommandManualTrigger;

        private readonly IDisposable? m_ReadOnlyNodesSub;
        private readonly IDisposable? m_ReadOnlyFlattenedFileNodesSub;
        private readonly IDisposable? m_SortUpdateSub;
        private readonly IDisposable? m_AreScenariosDisplayedSub;

        #endregion

        #region Ctors

        public ProjectScenarioManagerViewModel(
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
            m_FlattenedFileNodes = new();
            SelectedNodes = [];
            SelectedNode = null;
            m_ManagedNodeLookup = new();
            m_FileScenarioLookup = new();
            m_NodeTagLookup = new();
            m_NodeAction = new();
            m_NodeActionCommandManualTrigger = new();
            m_IsReadyToReviseTitle = ReadyToRevise.No;

            SetSelectedManagedNodesCommand = ReactiveCommand.Create<SelectionChangedEventArgs>(SetSelectedManagedNodes);
            {
                ReactiveCommand<IManagedNodeViewModel, Unit> loadProjectScenarioFileCommand = ReactiveCommand.CreateFromTask<IManagedNodeViewModel>(
                    LoadProjectScenarioFileAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null && !selectedNode.IsFolder),
                    RxApp.MainThreadScheduler);
                loadProjectScenarioFileCommand.IsExecuting.ToProperty(this, pm => pm.IsLoading, out m_IsLoading);
                LoadProjectScenarioFileCommand = loadProjectScenarioFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> loadSelectedProjectScenarioFileCommand = ReactiveCommand.CreateFromTask(
                    LoadSelectedProjectScenarioFileAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null && !selectedNode.IsFolder),
                    RxApp.MainThreadScheduler);
                loadSelectedProjectScenarioFileCommand.IsExecuting.ToProperty(this, pm => pm.IsLoading, out m_IsLoading);
                LoadSelectedProjectScenarioFileCommand = loadSelectedProjectScenarioFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> createEmptyProjectScenarioFileCommand = ReactiveCommand.CreateFromTask(
                    CreateEmptyProjectScenarioFileAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null),
                    RxApp.MainThreadScheduler);
                createEmptyProjectScenarioFileCommand.IsExecuting.ToProperty(this, pm => pm.IsCreating, out m_IsCreating);
                CreateEmptyProjectScenarioFileCommand = createEmptyProjectScenarioFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> createEmptyProjectScenarioFolderCommand = ReactiveCommand.CreateFromTask(
                    CreateEmptyProjectScenarioFolderAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null),
                    RxApp.MainThreadScheduler);
                createEmptyProjectScenarioFolderCommand.IsExecuting.ToProperty(this, pm => pm.IsCreating, out m_IsCreating);
                CreateEmptyProjectScenarioFolderCommand = createEmptyProjectScenarioFolderCommand;
            }
            {
                ReactiveCommand<Unit, Unit> renameProjectScenarioNodeCommand = ReactiveCommand.CreateFromTask(
                    RenameProjectScenarioNodeAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null),
                    RxApp.MainThreadScheduler);
                renameProjectScenarioNodeCommand.IsExecuting.ToProperty(this, pm => pm.IsRenaming, out m_IsRenaming);
                RenameProjectScenarioNodeCommand = renameProjectScenarioNodeCommand;
            }
            {
                ReactiveCommand<Unit, Unit> removeProjectScenarioNodeCommand = ReactiveCommand.CreateFromTask(
                    RemoveProjectScenarioNodeAsync,
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
                removeProjectScenarioNodeCommand.IsExecuting.ToProperty(this, pm => pm.IsRemoving, out m_IsRemoving);
                RemoveProjectScenarioNodeCommand = removeProjectScenarioNodeCommand;
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
                ReactiveCommand<Unit, Unit> cutProjectScenarioNodeCommand = ReactiveCommand.CreateFromTask(
                    CutProjectScenarioNodeAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null && !selectedNode.IsFolder)
                        .Merge(m_NodeActionCommandManualTrigger),
                    RxApp.MainThreadScheduler);
                CutProjectScenarioNodeCommand = cutProjectScenarioNodeCommand;
            }
            {
                ReactiveCommand<Unit, Unit> copyProjectScenarioNodeCommand = ReactiveCommand.CreateFromTask(
                    CopyProjectScenarioNodeAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null && !selectedNode.IsFolder)
                        .Merge(m_NodeActionCommandManualTrigger),
                    RxApp.MainThreadScheduler);
                CopyProjectScenarioNodeCommand = copyProjectScenarioNodeCommand;
            }
            {
                ReactiveCommand<Unit, Unit> pasteProjectScenarioNodeCommand = ReactiveCommand.CreateFromTask(
                    PasteProjectScenarioNodeAsync,
                    this.WhenAnyValue(
                        pm => pm.SelectedNode,
                        (IManagedNodeViewModel? selectedNode) => selectedNode is not null && m_NodeAction.NodeIds.Count != 0)
                        .Merge(m_NodeActionCommandManualTrigger),
                    RxApp.MainThreadScheduler);
                PasteProjectScenarioNodeCommand = pasteProjectScenarioNodeCommand;
            }

            ChangeSortModeCommand = ReactiveCommand.CreateFromTask<SortMode>(ChangeSortModeAsync);
            ChangeSortDirectionCommand = ReactiveCommand.CreateFromTask<SortDirection>(ChangeSortDirectionAsync);

            // Create read-only view to the source list.
            m_ReadOnlyNodesSub = m_Nodes.Connect()
                .AutoRefresh(node => node.Name) // Re-evaluates when this property changes.
                .AutoRefresh(node => node.CreatedOn)
                .AutoRefresh(node => node.ModifiedOn)
                .Sort(m_NodeSortComparer) // DynamicData listens to changes in this observable.
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(out m_ReadOnlyNodes)
                .Subscribe();

            // Create read-only view to the source list.
            m_ReadOnlyFlattenedFileNodesSub = m_FlattenedFileNodes.Connect()
                .AutoRefresh(node => node.IsTracked) // Re-evaluates when this property changes.
                .Bind(out m_ReadOnlyFlattenedFileNodes)
                .Subscribe();

            ResetRootNode();

            m_IsProjectScenarioUpdated = this
                .WhenAnyValue(pm => pm.m_CoreViewModel.IsProjectScenarioUpdated)
                .ToProperty(this, pm => pm.IsProjectScenarioUpdated);

            m_ProjectHasChanges = this
                .WhenAnyValue(
                    pm => pm.IsProjectUpdated,
                    pm => pm.IsProjectScenarioUpdated,
                    (isProjectUpdated, isProjectScenarioUpdated) => isProjectUpdated || isProjectScenarioUpdated)
                .ToProperty(this, pm => pm.ProjectHasChanges);

            m_SortUpdateSub = this
                .WhenAnyValue(
                    pm => pm.SelectedSortMode,
                    pm => pm.SelectedSortDirection)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(async _ => await ChangeSortAsync());

            m_AreScenariosDisplayedSub = m_ReadOnlyFlattenedFileNodes
                .ToObservableChangeSet()
                .AutoRefresh(node => node.IsTracked) // Subscribe only to IsCompiled property changes
                .Filter(node => !node.IsFolder && node.Scenario is not null)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(changeSet =>
                {
                    //if (!IsBusy && (changeSet.Replaced + changeSet.Adds) > 0)
                    //{
                    //    lock (m_Lock)
                    //    {
                    //        if (AutoCompile)
                    //        {
                    //            IsReadyToReviseTrackers = ReadyToRevise.Yes;
                    //            IsReadyToCompile = ReadyToCompile.Yes;
                    //        }
                    //        else
                    //        {
                    //            IsReadyToReviseTrackers = ReadyToRevise.No;
                    //            IsReadyToCompile = ReadyToCompile.No;
                    //        }
                    //    }
                    //}
                });

            Id = Resource.ProjectPlan.Titles.Title_ProjectScenarios;
            Title = Resource.ProjectPlan.Titles.Title_ProjectScenarios;
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
                    // Remember that Root does not go in the m_ManagedScenarioLookup.
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
                        new ProjectScenarioNodeModel
                        {
                            Id = rootId,
                            NodeType = ProjectScenarioNodeType.Folder,
                            Name = Resource.ProjectPlan.Labels.Label_RootNode,
                            CreatedOn = localNow,
                            ModifiedOn = localNow,
                            IsTracked = false,
                        });

                    AddTagLabels(
                        [
                            new ProjectScenarioTagModel
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

        /// <summary>
        /// Be sure to run this BEFORE you run AddManagedNodes.
        /// </summary>
        /// <param name="projectScenarioFileModels"></param>
        private void AddScenarioFiles(IEnumerable<ProjectScenarioFileModel> projectScenarioFileModels)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    foreach (ProjectScenarioFileModel projectScenarioFileModel in projectScenarioFileModels)
                    {
                        m_FileScenarioLookup[projectScenarioFileModel.NodeId] = projectScenarioFileModel;
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Be sure to run this AFTER you run RemoveManagedNodes.
        /// </summary>
        /// <param name="projectScenarioFileIds"></param>
        private void RemoveScenarioFiles(IEnumerable<Guid> projectScenarioFileIds)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    foreach (Guid projectScenarioFileId in projectScenarioFileIds)
                    {
                        m_FileScenarioLookup.TryRemove(projectScenarioFileId, out _);
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

        private void AddTagLabels(IEnumerable<ProjectScenarioTagModel> projectScenarioTagModels)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    foreach (ProjectScenarioTagModel tagModel in projectScenarioTagModels)
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

        private void ClearTagLabels(IEnumerable<ProjectScenarioTagModel> projectScenarioTagModels)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    foreach (ProjectScenarioTagModel tagModel in projectScenarioTagModels)
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

        private void RemoveTagLabels(IEnumerable<Guid> projectScenarioTagIds)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    foreach (Guid projectScenarioTagId in projectScenarioTagIds)
                    {
                        m_NodeTagLookup.TryRemove(projectScenarioTagId, out _);
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
                    m_FileScenarioLookup.Clear();
                    m_NodeTagLookup.Clear();
                    m_Nodes.Clear();
                    m_FlattenedFileNodes.Clear();
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

        private void AddManagedNodes(IEnumerable<ProjectScenarioNodeModel> managedNodes)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;

                    // First add all the scenarios to the lookup.
                    foreach (ProjectScenarioNodeModel projectScenarioNode in managedNodes)
                    {
                        if (!m_ManagedNodeLookup.ContainsKey(projectScenarioNode.Id))
                        {
                            var projectScenarioViewModel = new ManagedNodeViewModel(this, m_CoreViewModel, m_SettingService, m_NodeSortComparer, projectScenarioNode);

                            if (!projectScenarioViewModel.IsFolder
                                && m_FileScenarioLookup.TryGetValue(projectScenarioViewModel.Id, out ProjectScenarioFileModel? projectScenarioFile))
                            {
                                projectScenarioViewModel.Scenario = projectScenarioFile.Scenario;

                                // Keep the flattened file colleciton in-synch.
                                m_FlattenedFileNodes.Add(projectScenarioViewModel);
                            }

                            SetTagLabels(projectScenarioViewModel);
                            m_ManagedNodeLookup[projectScenarioViewModel.Id] = projectScenarioViewModel;
                        }
                    }

                    // Now build the hierarchy.
                    // Remember that the Root node is not in the lookup and forms the top-level parent.
                    foreach (ProjectScenarioNodeModel projectScenarioNode in managedNodes)
                    {
                        if (m_ManagedNodeLookup.TryGetValue(projectScenarioNode.Id, out IManagedNodeViewModel? managedNode))
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

                            if (m_ManagedNodeLookup.TryRemove(managedNode.Id, out IManagedNodeViewModel? projectScenarioViewModel))
                            {
                                m_FlattenedFileNodes.Remove(projectScenarioViewModel);
                            }

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
                    m_SettingService.SetProjectScenarioTitle(currentNode.Name);
                }
            }
        }

        private async Task LoadSelectedProjectScenarioFileAsync()
        {
            try
            {
                if (IsProjectScenarioUpdated)
                {
                    bool confirmation = await m_DialogService.ShowConfirmationAsync(
                        Resource.ProjectPlan.Titles.Title_ScenarioUnsavedChanges,
                        string.Empty,
                        Resource.ProjectPlan.Messages.Message_ScenarioUnsavedChanges);

                    if (!confirmation)
                    {
                        return;
                    }
                }
                IManagedNodeViewModel? managedNode = SelectedNode;
                await LoadProjectScenarioFileInternalAsync(managedNode);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task LoadProjectScenarioFileAsync(IManagedNodeViewModel? managedNodeViewModel)
        {
            try
            {
                if (managedNodeViewModel is null)
                {
                    return;
                }
                if (IsProjectScenarioUpdated)
                {
                    bool confirmation = await m_DialogService.ShowConfirmationAsync(
                        Resource.ProjectPlan.Titles.Title_ScenarioUnsavedChanges,
                        string.Empty,
                        Resource.ProjectPlan.Messages.Message_ScenarioUnsavedChanges);

                    if (!confirmation)
                    {
                        return;
                    }
                }
                await LoadProjectScenarioFileInternalAsync(managedNodeViewModel);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task LoadProjectScenarioFileInternalAsync(IManagedNodeViewModel? managedNodeViewModel) =>
            await Task.Run(() => LoadProjectScenarioFileInternal(managedNodeViewModel));

        private void LoadProjectScenarioFileInternal(IManagedNodeViewModel? managedNodeViewModel)
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
                        ProjectScenarioNodeModel selectedScenarioNodeModel = managedNodeViewModel.Node;
                        Guid nodeId = selectedScenarioNodeModel.Id;
                        string nodeName = selectedScenarioNodeModel.Name;

                        if (m_FileScenarioLookup.TryGetValue(nodeId, out ProjectScenarioFileModel? projectScenarioFile))
                        {
                            ProjectScenarioModel projectScenarioModel = projectScenarioFile.Scenario;
                            m_CoreViewModel.ProcessProjectScenario(projectScenarioModel, nodeId, nodeName);
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

        private async Task CreateEmptyProjectScenarioFileAsync()
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
                    title: Resource.ProjectPlan.Labels.Label_NewScenario,
                    header: string.Empty,
                    message: $@"**{Resource.ProjectPlan.Labels.Label_NewScenario}**",
                    context: nodeNameViewModel,
                    markdown: true);

                nodeNameViewModel.RunValidation();

                if (!result
                    || nodeNameViewModel.HasErrors)
                {
                    return;
                }

                await CreateEmptyProjectScenarioFileInternalAsync(parentId, nodeNameViewModel.Name);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task CreateEmptyProjectScenarioFileInternalAsync(Guid parentId, string nodeName) =>
            await Task.Run(() => CreateEmptyProjectScenarioFileInternal(parentId, nodeName));

        private void CreateEmptyProjectScenarioFileInternal(
            Guid parentId,
            string nodeName)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    ProjectScenarioModel projectScenario = m_CoreViewModel.CreateEmptyProjectScenario();
                    DateTimeOffset localNow = m_DateTimeCalculator.GetLocalNow();

                    var projectScenarioNode = new ProjectScenarioNodeModel
                    {
                        Id = Guid.NewGuid(),
                        ParentId = parentId,
                        NodeType = ProjectScenarioNodeType.File,
                        Name = nodeName,
                        CreatedOn = localNow,
                        ModifiedOn = localNow,
                        IsTracked = false,
                    };

                    var projectScenarioFile = new ProjectScenarioFileModel
                    {
                        NodeId = projectScenarioNode.Id,
                        Scenario = projectScenario,
                    };

                    AddScenarioFiles([projectScenarioFile]);
                    AddManagedNodes([projectScenarioNode]);
                    IsProjectUpdated = true;
                    IsReadyToReviseTitle = ReadyToRevise.Yes;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CreateEmptyProjectScenarioFolderAsync()
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
                    title: Resource.ProjectPlan.Labels.Label_NewScenarioFolder,
                    header: string.Empty,
                    message: $@"**{Resource.ProjectPlan.Labels.Label_NewScenarioFolder}**",
                    context: nodeNameViewModel,
                    markdown: true);

                nodeNameViewModel.RunValidation();

                if (!result
                    || nodeNameViewModel.HasErrors)
                {
                    return;
                }

                await CreateEmptyProjectScenarioFolderInternalAsync(parentId, nodeNameViewModel.Name);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task CreateEmptyProjectScenarioFolderInternalAsync(Guid parentId, string nodeName) =>
            await Task.Run(() => CreateEmptyProjectScenarioFolderInternal(parentId, nodeName));

        private void CreateEmptyProjectScenarioFolderInternal(Guid parentId, string nodeName)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    DateTimeOffset localNow = m_DateTimeCalculator.GetLocalNow();

                    var projectScenarioNode = new ProjectScenarioNodeModel
                    {
                        Id = Guid.NewGuid(),
                        ParentId = parentId,
                        NodeType = ProjectScenarioNodeType.Folder,
                        Name = nodeName,
                        CreatedOn = localNow,
                        ModifiedOn = localNow,
                        IsTracked = false,
                    };

                    AddManagedNodes([projectScenarioNode]);
                    IsProjectUpdated = true;
                    IsReadyToReviseTitle = ReadyToRevise.Yes;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RenameProjectScenarioNodeAsync()
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

                // update the scenario title setting if the renamed node is the currently loaded scenario.
                if (managedNode.Id == m_SettingService.ScenarioId)
                {
                    m_SettingService.SetProjectScenarioTitle(managedNode.Name);
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

        private async Task RemoveProjectScenarioNodeAsync()
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

                await RemoveProjectScenarioNodeInternalAsync(managedNodes.Select(x => x.Id));
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private async Task RemoveProjectScenarioNodeInternalAsync(IEnumerable<Guid> nodeIds) =>
            await Task.Run(() => RemoveProjectScenarioNodeInternal(nodeIds));

        private void RemoveProjectScenarioNodeInternal(IEnumerable<Guid> nodeIds)
        {
            try
            {
                lock (m_Lock)
                {
                    IsBusy = true;
                    HashSet<IManagedNodeViewModel> nestedNodes = FindNestedNodes(nodeIds);

                    RemoveManagedNodes(nestedNodes);
                    RemoveScenarioFiles([.. nestedNodes.Where(x => !x.IsFolder).Select(x => x.Id)]);
                    RemoveTagLabels([.. nestedNodes.Select(x => x.Id)]);

                    // If the managed scenario being removed is the currently loaded scenario, then
                    // reset the core project scenario to the most recently added scenario, if any.
                    // If there are no remaining scenarios, then create a new blank project scenario.

                    Guid currentNodeId = m_SettingService.ScenarioId;
                    HashSet<Guid> nestedNodeIds = [.. nestedNodes.Select(n => n.Id).Distinct()];

                    if (!nestedNodeIds.Contains(currentNodeId))
                    {
                        IsProjectUpdated = true;
                        IsReadyToReviseTitle = ReadyToRevise.Yes;
                        return;
                    }

                    // Find the most recently modified scenario that is not being removed.
                    IManagedNodeViewModel? mostRecentScenario = m_ManagedNodeLookup.Values
                        .Where(x => !x.IsFolder)
                        .OrderByDescending(x => x.Node.ModifiedOn)
                        .FirstOrDefault();

                    // If found, load it.
                    if (mostRecentScenario is not null)
                    {
                        LoadProjectScenarioFileInternal(mostRecentScenario);
                        IsProjectUpdated = true;
                        IsReadyToReviseTitle = ReadyToRevise.Yes;
                        return;
                    }

                    // Otherwise, we need to create a new blank project scenario.

                    // Reset the core project scenario.
                    m_CoreViewModel.ResetProjectScenario();

                    // Now add the new core project scenario to the project manager.
                    ProjectScenarioModel projectScenario = m_CoreViewModel.BuildProjectScenario();
                    DateTimeOffset localNow = m_DateTimeCalculator.GetLocalNow();

                    var projectScenarioNode = new ProjectScenarioNodeModel
                    {
                        Id = m_SettingService.ScenarioId,
                        ParentId = Root.Id,
                        NodeType = ProjectScenarioNodeType.File,
                        Name = Resource.ProjectPlan.Labels.Label_BaseNode,
                        CreatedOn = localNow,
                        ModifiedOn = localNow,
                        IsTracked = false,
                    };

                    var projectScenarioFile = new ProjectScenarioFileModel
                    {
                        NodeId = projectScenarioNode.Id,
                        Scenario = projectScenario,
                    };

                    AddTagLabels([]); // Don't really need this, but for consistency.
                    AddScenarioFiles([projectScenarioFile]);
                    AddManagedNodes([projectScenarioNode]);

                    MarkNodeAsLoaded(projectScenarioNode.Id);

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

        private async Task CutProjectScenarioNodeAsync()
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

        private async Task CopyProjectScenarioNodeAsync()
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

        private async Task PasteProjectScenarioNodeAsync()
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

                await PasteProjectScenarioNodeInternalAsync(destinationParentId);
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

        private async Task PasteProjectScenarioNodeInternalAsync(Guid destinationParentId) =>
            await Task.Run(() => PasteProjectScenarioNodeInternal(destinationParentId));

        private void PasteProjectScenarioNodeInternal(Guid destinationParentId)
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
                            && managedNode.Scenario is not null)
                        {
                            var projectScenarioNode = new ProjectScenarioNodeModel
                            {
                                Id = Guid.NewGuid(),
                                ParentId = destinationParentId,
                                NodeType = ProjectScenarioNodeType.File,
                                Name = managedNode.Name,
                                CreatedOn = localNow,
                                ModifiedOn = localNow,
                                IsTracked = managedNode.IsTracked,
                            };

                            var projectScenarioFile = new ProjectScenarioFileModel
                            {
                                NodeId = projectScenarioNode.Id,
                                Scenario = managedNode.Scenario.CloneObject(),
                            };

                            AddTagLabels([]); // Don't really need this, but for consistency.
                            AddScenarioFiles([projectScenarioFile]);
                            AddManagedNodes([projectScenarioNode]);

                            // Post paste action.
                            NodeAction action = m_NodeAction.Action;
                            switch (action)
                            {
                                case NodeAction.Cut:
                                    RemoveProjectScenarioNodeInternal([nodeId]);
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

                var tagModel = new ProjectScenarioTagModel
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

        private async Task AddNodeTagInternalAsync(ProjectScenarioTagModel tagModel, IManagedNodeViewModel managedNodeViewModel) =>
            await Dispatcher.UIThread.InvokeAsync(() => AddNodeTagInternal(tagModel, managedNodeViewModel));

        private void AddNodeTagInternal(
            ProjectScenarioTagModel tagModel,
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

                IList<ProjectScenarioTagModel> tagModels = [.. labels
                    .Select(label => new ProjectScenarioTagModel
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

        private async Task RemoveNodeTagInternalAsync(ProjectScenarioTagModel tagModel, IManagedNodeViewModel managedNodeViewModel) =>
            await Dispatcher.UIThread.InvokeAsync(() => RemoveNodeTagInternal(tagModel, managedNodeViewModel));

        private void RemoveNodeTagInternal(
            ProjectScenarioTagModel tagModel,
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

        #region IProjectScenarioManagerViewModel

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

        private readonly ObservableAsPropertyHelper<bool> m_IsProjectScenarioUpdated;
        public bool IsProjectScenarioUpdated => m_IsProjectScenarioUpdated.Value;

        private readonly ObservableAsPropertyHelper<bool> m_ProjectHasChanges;
        public bool ProjectHasChanges => m_ProjectHasChanges.Value;

        public IManagedNodeViewModel Root { get; private set; }

        private readonly SourceList<IManagedNodeViewModel> m_Nodes;
        public IReadOnlyList<IManagedNodeViewModel> RawNodes => m_Nodes.Items;

        private readonly ReadOnlyObservableCollection<IManagedNodeViewModel> m_ReadOnlyNodes;
        public ReadOnlyObservableCollection<IManagedNodeViewModel> Nodes => m_ReadOnlyNodes;




        private readonly SourceList<IManagedNodeViewModel> m_FlattenedFileNodes;
        public IReadOnlyList<IManagedNodeViewModel> RawFlattenedFileNodes => m_FlattenedFileNodes.Items;

        private readonly ReadOnlyObservableCollection<IManagedNodeViewModel> m_ReadOnlyFlattenedFileNodes;
        public ReadOnlyObservableCollection<IManagedNodeViewModel> FlattenedFileNodes => m_ReadOnlyFlattenedFileNodes;









        public ObservableCollection<IManagedNodeViewModel> SelectedNodes { get; }

        private IManagedNodeViewModel? m_SelectedNode;
        public IManagedNodeViewModel? SelectedNode
        {
            get => m_SelectedNode;
            private set => this.RaiseAndSetIfChanged(ref m_SelectedNode, value);
        }

        public SortMode SelectedSortMode
        {
            get => m_SettingService.ProjectScenarioSortMode;
            set
            {
                lock (m_Lock)
                {
                    m_SettingService.ProjectScenarioSortMode = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public SortDirection SelectedSortDirection
        {
            get => m_SettingService.ProjectScenarioSortDirection;
            set
            {
                lock (m_Lock)
                {
                    m_SettingService.ProjectScenarioSortDirection = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public ICommand SetSelectedManagedNodesCommand { get; }

        public ICommand LoadProjectScenarioFileCommand { get; }

        public ICommand LoadSelectedProjectScenarioFileCommand { get; }

        public ICommand CreateEmptyProjectScenarioFileCommand { get; }

        public ICommand CreateEmptyProjectScenarioFolderCommand { get; }

        public ICommand RenameProjectScenarioNodeCommand { get; }

        public ICommand RemoveProjectScenarioNodeCommand { get; }

        public ICommand CutProjectScenarioNodeCommand { get; }

        public ICommand CopyProjectScenarioNodeCommand { get; }

        public ICommand PasteProjectScenarioNodeCommand { get; }

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

                    // Reset the core project scenario.
                    m_CoreViewModel.ResetProjectScenario();

                    // Reset the project manager.
                    ClearProject();
                    ResetRootNode();

                    m_SettingService.ResetProject();

                    // Now add the new core project scenario to the project manager.
                    ProjectScenarioModel projectScenario = m_CoreViewModel.BuildProjectScenario();
                    DateTimeOffset localNow = m_DateTimeCalculator.GetLocalNow();

                    var projectScenarioNode = new ProjectScenarioNodeModel
                    {
                        Id = m_SettingService.ScenarioId,
                        ParentId = Root.Id,
                        NodeType = ProjectScenarioNodeType.File,
                        Name = Resource.ProjectPlan.Labels.Label_BaseNode,
                        CreatedOn = localNow,
                        ModifiedOn = localNow,
                        IsTracked = false,
                    };

                    var projectScenarioFile = new ProjectScenarioFileModel
                    {
                        NodeId = projectScenarioNode.Id,
                        Scenario = projectScenario,
                    };

                    AddTagLabels([]); // Don't really need this, but for consistency.
                    AddScenarioFiles([projectScenarioFile]);
                    AddManagedNodes([projectScenarioNode]);

                    MarkNodeAsLoaded(projectScenarioNode.Id);

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

                    AddScenarioFiles(projectModel.Files);

                    AddManagedNodes(projectModel.Nodes);

                    // Now process the current project scenario.

                    // If the list is empty, then create a new blank project scenario and
                    // add it to the project.
                    if (projectModel.Nodes.Count == 0)
                    {
                        m_CoreViewModel.ResetProjectScenario();
                        ProjectScenarioModel projectScenario = m_CoreViewModel.BuildProjectScenario();
                        DateTimeOffset localNow = m_DateTimeCalculator.GetLocalNow();

                        var projectScenarioNode = new ProjectScenarioNodeModel
                        {
                            Id = m_SettingService.ScenarioId,
                            ParentId = Root.Id,
                            NodeType = ProjectScenarioNodeType.File,
                            Name = Resource.ProjectPlan.Labels.Label_BaseNode,
                            CreatedOn = localNow,
                            ModifiedOn = localNow,
                            IsTracked = false,
                        };

                        var projectScenarioFile = new ProjectScenarioFileModel
                        {
                            NodeId = projectScenarioNode.Id,
                            Scenario = projectScenario,
                        };

                        AddTagLabels([]); // Don't really need this, but for consistency.
                        AddScenarioFiles([projectScenarioFile]);
                        AddManagedNodes([projectScenarioNode]);
                    }
                    // Otherwise, load the project listed as current, if it exists.
                    else if (m_FileScenarioLookup.TryGetValue(projectModel.Current, out ProjectScenarioFileModel? projectScenarioFileModel))
                    {
                        // Load the current project scenario.
                        Guid projectScenarioId = projectScenarioFileModel.NodeId;
                        ProjectScenarioModel projectScenarioModel = projectScenarioFileModel.Scenario;
                        IManagedNodeViewModel? projectScenarioNode = GetNode(projectScenarioId);
                        string projectScenarioName = projectScenarioNode?.Name ?? Resource.ProjectPlan.Labels.Label_UnknownNode;

                        m_CoreViewModel.ProcessProjectScenario(projectScenarioModel, projectScenarioId, projectScenarioName);
                        MarkNodeAsLoaded(projectScenarioId);
                    }
                    // Otherwise, load the latest project scenario.
                    else
                    {
                        ProjectScenarioNodeModel latestScenarioNodeModel = projectModel.Nodes.Last();
                        Guid latestProjectScenarioId = latestScenarioNodeModel.Id;
                        string latestProjectScenarioName = latestScenarioNodeModel.Name;

                        if (m_FileScenarioLookup.TryGetValue(latestProjectScenarioId, out ProjectScenarioFileModel? latestProjectScenarioFile))
                        {
                            ProjectScenarioModel latestProjectScenarioModel = latestProjectScenarioFile.Scenario;
                            m_CoreViewModel.ProcessProjectScenario(latestProjectScenarioModel, latestProjectScenarioId, latestProjectScenarioName);
                            MarkNodeAsLoaded(latestProjectScenarioId);
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

                    // Ensure that the current project scenario is up to date.

                    Guid nodeId = m_SettingService.ScenarioId;
                    ProjectScenarioModel projectScenario = m_CoreViewModel.BuildProjectScenario();

                    IManagedNodeViewModel? managedProjectScenario = GetNode(nodeId);
                    DateTimeOffset localNow = m_DateTimeCalculator.GetLocalNow();

                    if (managedProjectScenario is null)
                    {
                        // No existing managed scenario, so add it to the Root.

                        var projectScenarioNode = new ProjectScenarioNodeModel
                        {
                            Id = nodeId,
                            ParentId = Root.Id,
                            NodeType = ProjectScenarioNodeType.File,
                            Name = Resource.ProjectPlan.Labels.Label_BaseNode,
                            CreatedOn = localNow,
                            ModifiedOn = localNow,
                            IsTracked = false,
                        };

                        var projectScenarioFile = new ProjectScenarioFileModel
                        {
                            NodeId = projectScenarioNode.Id,
                            Scenario = projectScenario,
                        };

                        AddScenarioFiles([projectScenarioFile]);
                        AddManagedNodes([projectScenarioNode]);
                    }
                    else
                    {
                        // Update existing managed scenario.
                        managedProjectScenario.Scenario = projectScenario;
                        managedProjectScenario.ModifiedOn = localNow;

                        var projectScenarioFile = new ProjectScenarioFileModel
                        {
                            NodeId = managedProjectScenario.Id,
                            Scenario = projectScenario,
                        };

                        RemoveScenarioFiles([projectScenarioFile.NodeId]);
                        AddScenarioFiles([projectScenarioFile]);
                    }

                    // Now build the ProjectModel.

                    Guid projectId = m_SettingService.ProjectId;
                    Guid rootId = Root.Id;
                    Guid currentId = m_SettingService.ScenarioId;
                    List<ProjectScenarioNodeModel> nodes = [.. m_ManagedNodeLookup.Values
                        .Select(x => x.Node)
                        .OrderBy(x => x.CreatedOn)];

                    List<ProjectScenarioFileModel> files = [.. m_FileScenarioLookup.Values];

                    // Filter out any tags that apply to the Root node.
                    List<ProjectScenarioTagModel> tags = [.. m_NodeTagLookup
                        .Where(kvp => kvp.Key != rootId)
                        .SelectMany(kvp => kvp.Value.Select(
                            label => new ProjectScenarioTagModel
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
            m_ReadOnlyNodesSub?.Dispose();
            m_ReadOnlyFlattenedFileNodesSub?.Dispose();
            m_SortUpdateSub?.Dispose();
            m_AreScenariosDisplayedSub?.Dispose();
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
                m_IsProjectScenarioUpdated?.Dispose();
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
