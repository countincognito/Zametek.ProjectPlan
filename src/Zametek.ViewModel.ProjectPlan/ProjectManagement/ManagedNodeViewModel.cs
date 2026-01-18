using DynamicData;
using ReactiveUI;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Linq;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ManagedNodeViewModel
        : ViewModelBase, IManagedNodeViewModel, IEditableObject, INotifyDataErrorInfo
    {
        #region Fields

        private readonly object m_Lock;
        private ProjectPlanNodeModel m_ProjectPlanNodeModel;
        private ProjectPlanModel? m_ProjectPlanModel;

        private static readonly string[] s_NoErrors = [];
        private readonly Dictionary<string, List<string>> m_ErrorsByPropertyName;

        #endregion

        #region Ctors

        public ManagedNodeViewModel()
            : this(new ProjectPlanNodeModel())
        {
        }

        public ManagedNodeViewModel(
            ProjectPlanNodeModel projectPlanNode,
            ProjectPlanModel projectPlan)
            : this(projectPlanNode)
        {
            ArgumentNullException.ThrowIfNull(projectPlan);
            m_ProjectPlanModel = projectPlan;
        }

        public ManagedNodeViewModel(ProjectPlanNodeModel projectPlanNode)
        {
            ArgumentNullException.ThrowIfNull(projectPlanNode);
            m_Lock = new object();
            m_IsLoaded = false;
            m_Labels = new();

            // Create read-only view to the source list.
            m_Labels.Connect()
               .ObserveOn(RxApp.MainThreadScheduler)
               .Bind(out m_ReadOnlyLabels)
               .Subscribe();

            m_ProjectPlanNodeModel = projectPlanNode;
            m_ProjectPlanModel = null;
            m_Children = new();

            // Create read-only view to the source list.
            m_Children.Connect()
               .ObserveOn(RxApp.MainThreadScheduler)
               .Bind(out m_ReadOnlyChildren)
               .Subscribe();

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

        public Guid Id => m_ProjectPlanNodeModel.Id;

        public Guid ParentId
        {
            get
            {
                return m_ProjectPlanNodeModel.ParentId;
            }
            set
            {
                m_ProjectPlanNodeModel = m_ProjectPlanNodeModel with { ParentId = value };
            }
        }

        public bool IsFolder => m_ProjectPlanNodeModel.IsFolder;

        public string Name
        {
            get
            {
                return m_ProjectPlanNodeModel.Name;
            }
            set
            {
                m_ProjectPlanNodeModel = m_ProjectPlanNodeModel with { Name = value };
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(DisplayName));
            }
        }

        public DateTimeOffset CreatedOn => m_ProjectPlanNodeModel.CreatedOn;

        public DateTimeOffset ModifiedOn
        {
            get
            {
                return m_ProjectPlanNodeModel.ModifiedOn;
            }
            set
            {
                m_ProjectPlanNodeModel = m_ProjectPlanNodeModel with { ModifiedOn = value };
            }
        }

        public ProjectPlanModel? ProjectPlan
        {
            get
            {
                return m_ProjectPlanModel;
            }
            set
            {
                if (IsFolder)
                {
                    throw new InvalidOperationException($@"{Resource.ProjectPlan.Messages.Message_CannotSetProjectPlanOnFolderNode} {Id}");
                }
                m_ProjectPlanModel = value;
            }
        }

        public ProjectPlanNodeModel Node => m_ProjectPlanNodeModel;

        public ProjectPlanFileModel File
        {
            get
            {
                if (IsFolder)
                {
                    throw new InvalidOperationException($@"{Resource.ProjectPlan.Messages.Message_CannotGetProjectPlanFileFromFolderNode} {Id}");
                }
                if (m_ProjectPlanModel is null)
                {
                    throw new InvalidOperationException($@"{Resource.ProjectPlan.Messages.Message_CannotGetProjectPlanFileWhenProjectPlanIsNull} {Id}");
                }
                return new ProjectPlanFileModel
                {
                    NodeId = m_ProjectPlanNodeModel.Id,
                    Plan = m_ProjectPlanModel,
                };
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
                return $@"[{string.Join(DependenciesStringValidationRule.Separator, RawLabels)}]";
            }
        }

        public string DisplayName
        {
            get
            {
                if (!IsFolder)
                {
                    return Name;
                }
                return $@"[{Name}]";
            }
        }

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
                        node.Dispose();
                        children.Remove(node);
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
