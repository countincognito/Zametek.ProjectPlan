using DynamicData;
using ReactiveUI;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ManagedNodeViewModel
        : ViewModelBase, IManagedNodeViewModel, IEditableObject, INotifyDataErrorInfo
    {
        #region Fields

        private readonly object m_Lock;
        private readonly IProjectScenarioManagerViewModel m_ProjectScenarioManagerViewModel;
        private readonly ICoreViewModel m_CoreViewModel;
        private readonly ISettingService m_SettingService;
        private readonly BehaviorSubject<IComparer<IManagedNodeViewModel>> m_NodeSortComparer;
        private ProjectScenarioNodeModel m_ProjectScenarioNodeModel;
        private ProjectScenarioModel? m_ProjectScenarioModel;

        private static readonly string[] s_NoErrors = [];
        private readonly Dictionary<string, List<string>> m_ErrorsByPropertyName;

        private readonly IDisposable m_ReadOnlyLabelsSub;
        private readonly IDisposable m_ReadOnlyChildrenSub;

        #endregion

        #region Ctors

        public ManagedNodeViewModel(
            IProjectScenarioManagerViewModel projectScenarioManagerViewModel,
            ICoreViewModel coreViewModel,
            ISettingService settingService,
            BehaviorSubject<IComparer<IManagedNodeViewModel>> nodeSortComparer)
            : this(projectScenarioManagerViewModel, coreViewModel, settingService, nodeSortComparer, new ProjectScenarioNodeModel())
        {
        }

        public ManagedNodeViewModel(
            IProjectScenarioManagerViewModel projectScenarioManagerViewModel,
            ICoreViewModel coreViewModel,
            ISettingService settingService,
            BehaviorSubject<IComparer<IManagedNodeViewModel>> nodeSortComparer,
            ProjectScenarioNodeModel projectScenarioNode,
            ProjectScenarioModel projectScenario)
            : this(projectScenarioManagerViewModel, coreViewModel, settingService, nodeSortComparer, projectScenarioNode)
        {
            ArgumentNullException.ThrowIfNull(projectScenario);
            m_ProjectScenarioModel = projectScenario;
        }

        public ManagedNodeViewModel(
            IProjectScenarioManagerViewModel projectScenarioManagerViewModel,
            ICoreViewModel coreViewModel,
            ISettingService settingService,
            BehaviorSubject<IComparer<IManagedNodeViewModel>> nodeSortComparer,
            ProjectScenarioNodeModel projectScenarioNode)
        {
            ArgumentNullException.ThrowIfNull(projectScenarioManagerViewModel);
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(nodeSortComparer);
            ArgumentNullException.ThrowIfNull(projectScenarioNode);
            m_Lock = new object();
            m_IsLoaded = false;
            m_Labels = new();

            // Create read-only view to the source list.
            m_ReadOnlyLabelsSub = m_Labels.Connect()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(out m_ReadOnlyLabels)
                .Subscribe();

            m_ProjectScenarioManagerViewModel = projectScenarioManagerViewModel;
            m_CoreViewModel = coreViewModel;
            m_SettingService = settingService;
            m_NodeSortComparer = nodeSortComparer;
            m_ProjectScenarioNodeModel = projectScenarioNode;
            m_ProjectScenarioModel = null;
            m_Children = new();

            // Create read-only view to the source list.
            m_ReadOnlyChildrenSub = m_Children.Connect()
                .AutoRefresh(node => node.Name) // Re-evaluates when this property changes.
                .AutoRefresh(node => node.CreatedOn)
                .AutoRefresh(node => node.ModifiedOn)
                .Sort(m_NodeSortComparer)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(out m_ReadOnlyChildren)
                .Subscribe();

            m_DisplayName = this
                .WhenAnyValue(
                    x => x.m_ProjectScenarioManagerViewModel.IsProjectUpdated,
                    x => x.m_CoreViewModel.IsProjectScenarioUpdated,
                    x => x.Name,
                    x => x.Label,
                    (isProjectUpdated, isProjectScenarioUpdated, name, label) =>
                    {
                        string displayName = name;
                        if (IsFolder)
                        {
                            displayName = $@"[{name}]";
                        }
                        Guid projectScenarioId = m_SettingService.ScenarioId;
                        bool nodeHasChanges = m_ProjectScenarioNodeModel.Id == projectScenarioId && isProjectScenarioUpdated;
                        return $@"{(nodeHasChanges ? "*" : string.Empty)}{displayName} {label}";
                    })
                .ToProperty(this, x => x.DisplayName);

            m_ErrorsByPropertyName = [];
        }

        #endregion

        #region Properties

        #endregion

        #region Private Methods

        private void SetError(string propertyName, string error)
        {
            if (m_ErrorsByPropertyName.TryGetValue(propertyName, out List<string>? errorList))
            {
                if (!errorList.Contains(error))
                {
                    errorList.Add(error);
                }
            }
            else
            {
                m_ErrorsByPropertyName.Add(propertyName, [error]);
            }
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            this.RaisePropertyChanged(nameof(HasErrors));
        }

        private void ClearErrors(string? propertyName)
        {
            if (!string.IsNullOrWhiteSpace(propertyName)
                && m_ErrorsByPropertyName.TryGetValue(propertyName, out List<string>? errorList))
            {
                errorList.Clear();
            }
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        private void ClearErrors()
        {
            IList<string> propertyNames = [.. m_ErrorsByPropertyName.Keys];
            m_ErrorsByPropertyName.Clear();

            foreach (string propertyName in propertyNames)
            {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            }

            this.RaisePropertyChanged(nameof(HasErrors));
        }

        #endregion

        #region IManagedNodeViewModel Members

        public Guid Id => m_ProjectScenarioNodeModel.Id;

        public Guid ParentId
        {
            get
            {
                return m_ProjectScenarioNodeModel.ParentId;
            }
            set
            {
                m_ProjectScenarioNodeModel = m_ProjectScenarioNodeModel with { ParentId = value };
            }
        }

        public bool IsFolder => m_ProjectScenarioNodeModel.NodeType == ProjectScenarioNodeType.Folder;

        public string Name
        {
            get
            {
                return m_ProjectScenarioNodeModel.Name;
            }
            set
            {
                m_ProjectScenarioNodeModel = m_ProjectScenarioNodeModel with { Name = value };
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(DisplayName));
            }
        }

        public DateTimeOffset CreatedOn => m_ProjectScenarioNodeModel.CreatedOn;

        public DateTimeOffset ModifiedOn
        {
            get
            {
                return m_ProjectScenarioNodeModel.ModifiedOn;
            }
            set
            {
                m_ProjectScenarioNodeModel = m_ProjectScenarioNodeModel with { ModifiedOn = value };
                this.RaisePropertyChanged();
            }
        }

        public ProjectScenarioModel? Scenario
        {
            get
            {
                return m_ProjectScenarioModel;
            }
            set
            {
                if (IsFolder)
                {
                    throw new InvalidOperationException($@"{Resource.ProjectPlan.Messages.Message_CannotSetProjectScenarioOnFolderNode} {Id}");
                }
                m_ProjectScenarioModel = value;
            }
        }

        public ProjectScenarioNodeModel Node => m_ProjectScenarioNodeModel;

        public ProjectScenarioFileModel File
        {
            get
            {
                if (IsFolder)
                {
                    throw new InvalidOperationException($@"{Resource.ProjectPlan.Messages.Message_CannotGetProjectScenarioFileFromFolderNode} {Id}");
                }
                if (m_ProjectScenarioModel is null)
                {
                    throw new InvalidOperationException($@"{Resource.ProjectPlan.Messages.Message_CannotGetProjectScenarioFileWhenProjectScenarioIsNull} {Id}");
                }
                return new ProjectScenarioFileModel
                {
                    NodeId = m_ProjectScenarioNodeModel.Id,
                    Scenario = m_ProjectScenarioModel,
                };
            }
        }

        private bool m_IsTracked;
        public bool IsTracked
        {
            get
            {
                if (IsFolder)
                {
                    return false;
                }
                return m_IsTracked;
            }
            set
            {
                if (IsFolder)
                {
                    throw new InvalidOperationException($@"{Resource.ProjectPlan.Messages.Message_CannotTrackProjectScenarioFileFromFolderNode} {Id}");
                }
                m_IsTracked = value;
                this.RaisePropertyChanged();
            }
        }

        private bool m_IsLoaded;
        public bool IsLoaded
        {
            get => m_IsLoaded;
            set
            {
                m_IsLoaded = value;
                this.RaisePropertyChanged();
            }
        }

        private readonly SourceList<string> m_Labels;
        public IReadOnlyList<string> RawLabels => m_Labels.Items;

        private readonly ReadOnlyObservableCollection<string> m_ReadOnlyLabels;
        public ReadOnlyObservableCollection<string> Labels => m_ReadOnlyLabels;

        public void SetLabels(IEnumerable<string> labels)
        {
            lock (m_Lock)
            {
                m_Labels.Edit(list =>
                {
                    list.Clear();
                    list.AddRange(labels);
                });

                this.RaisePropertyChanged(nameof(Label));
                this.RaisePropertyChanged(nameof(DisplayName));
            }
        }

        public string Label
        {
            get
            {
                if (RawLabels.Count == 0)
                {
                    return string.Empty;
                }
                return $@"({string.Join(DependenciesStringValidationRule.Separator, RawLabels)})";
            }
        }

        private readonly ObservableAsPropertyHelper<string> m_DisplayName;
        public string DisplayName => m_DisplayName.Value;

        private readonly SourceList<IManagedNodeViewModel> m_Children;
        public IReadOnlyList<IManagedNodeViewModel> RawChildren => m_Children.Items;

        private readonly ReadOnlyObservableCollection<IManagedNodeViewModel> m_ReadOnlyChildren;
        public ReadOnlyObservableCollection<IManagedNodeViewModel> Children => m_ReadOnlyChildren;

        public void AddChildren(IEnumerable<IManagedNodeViewModel> managedNodes)
        {
            lock (m_Lock)
            {
                m_Children.Edit(children =>
                {
                    foreach (IManagedNodeViewModel managedNode in managedNodes)
                    {
                        children.Add(managedNode);
                    }
                });
            }
        }

        public void RemoveChildren(IEnumerable<Guid> managedNodeIds)
        {
            lock (m_Lock)
            {
                m_Children.Edit(children =>
                {
                    IList<IManagedNodeViewModel> nodes = [.. RawChildren.Where(x => managedNodeIds.Contains(x.Id))];

                    foreach (IManagedNodeViewModel node in nodes)
                    {
                        children.Remove(node);
                        node.Dispose();
                    }
                });
            }
        }

        public void ClearChildren()
        {
            lock (m_Lock)
            {
                m_Children.Edit(children =>
                {
                    foreach (IManagedNodeViewModel node in RawChildren)
                    {
                        node.Dispose();
                    }
                    children.Clear();
                });
            }
        }

        #endregion

        #region IEditableObject Members

        private bool m_isDirty;

        public void BeginEdit()
        {
            // Bug Fix: Windows Controls call EndEdit twice; Once
            // from IEditableCollectionView, and once from BindingGroup.
            // This makes sure it only happens once after a BeginEdit.
            m_isDirty = true;
        }

        public void EndEdit()
        {
            if (m_isDirty)
            {
                m_isDirty = false;
            }
        }

        public void CancelEdit()
        {
            m_isDirty = false;
        }

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
            m_ReadOnlyLabelsSub?.Dispose();
            m_ReadOnlyChildrenSub?.Dispose();
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
                KillSubscriptions();
                m_Labels.Clear();
                m_Labels.Dispose();
                ClearChildren();
                m_Children.Dispose();
                m_DisplayName?.Dispose();
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

        #region INotifyDataErrorInfo Members

        public bool HasErrors => m_ErrorsByPropertyName.Count != 0;

        public IEnumerable GetErrors(string? propertyName)
        {
            if (!string.IsNullOrWhiteSpace(propertyName)
                && m_ErrorsByPropertyName.TryGetValue(propertyName, out List<string>? errorList))
            {
                return errorList;
            }
            return s_NoErrors;
        }

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        #endregion
    }
}
