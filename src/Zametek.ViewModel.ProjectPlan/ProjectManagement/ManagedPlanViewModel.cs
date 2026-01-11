using DynamicData;
using ReactiveUI;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Linq;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ManagedPlanViewModel
        : ViewModelBase, IManagedPlanViewModel, IEditableObject, INotifyDataErrorInfo
    {
        #region Fields

        private readonly object m_Lock;
        private ProjectPlanNodeModel m_ProjectPlanNodeModel;

        private static readonly string[] s_NoErrors = [];
        private readonly Dictionary<string, List<string>> m_ErrorsByPropertyName;

        private const int c_IdSizeLimit = 8;

        #endregion

        #region Ctors

        public ManagedPlanViewModel()
            : this(new ProjectPlanNodeModel())
        {
        }

        public ManagedPlanViewModel(ProjectPlanNodeModel projectPlanNode)
        {
            ArgumentNullException.ThrowIfNull(projectPlanNode);
            m_Lock = new object();
            m_Labels = new();

            // Create read-only view to the source list.
            m_Labels.Connect()
               .ObserveOn(RxApp.MainThreadScheduler)
               .Bind(out m_ReadOnlyLabels)
               .Subscribe();

            m_ProjectPlanNodeModel = projectPlanNode;
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

        #region IManagedPlanViewModel Members

        public Guid Id => m_ProjectPlanNodeModel.Id;

        public Guid ParentId => m_ProjectPlanNodeModel.ParentId;

        public string Comment
        {
            get
            {
                return m_ProjectPlanNodeModel.Comment;
            }
            set
            {
                m_ProjectPlanNodeModel = m_ProjectPlanNodeModel with { Comment = value };
            }
        }

        public ProjectPlanModel ProjectPlan
        {
            get
            {
                return m_ProjectPlanNodeModel.ProjectPlan;
            }
            set
            {
                m_ProjectPlanNodeModel = m_ProjectPlanNodeModel with { ProjectPlan = value };
            }
        }

        public ProjectPlanNodeModel Node => m_ProjectPlanNodeModel;

        private readonly SourceList<string> m_Labels;
        private readonly ReadOnlyObservableCollection<string> m_ReadOnlyLabels;
        public ReadOnlyObservableCollection<string> Labels => m_ReadOnlyLabels;

        public void SetLabels(IEnumerable<string> labels)
        {
            try
            {
                lock (m_Lock)
                {
                    m_Labels.Edit(list =>
                    {
                        list.Clear();
                        list.AddRange(labels);
                    });

                    this.RaisePropertyChanged(nameof(Label));

                    //IsProjectUpdated = true;
                }
            }
            finally
            {
            }
        }

        public string Label
        {
            get
            {
                string idString = Id.ToFlatString()[..c_IdSizeLimit];

                if (Labels.Count == 0)
                {
                    return idString;
                }
                return $@"[{string.Join(DependenciesStringValidationRule.Separator, Labels)}] ({idString})";
            }
        }

        private readonly SourceList<IManagedPlanViewModel> m_Children;
        private readonly ReadOnlyObservableCollection<IManagedPlanViewModel> m_ReadOnlyChildren;
        public ReadOnlyObservableCollection<IManagedPlanViewModel> Children => m_ReadOnlyChildren;

        public void AddChildren(IEnumerable<IManagedPlanViewModel> managedPlans)
        {
            try
            {
                lock (m_Lock)
                {
                    m_Children.Edit(children =>
                    {
                        foreach (IManagedPlanViewModel managedPlan in managedPlans)
                        {
                            children.Add(managedPlan);
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
                    });

                    //IsProjectUpdated = true;
                }
            }
            finally
            {
            }
        }



        //void AddChildren(IEnumerable<ProjectPlanNodeModel> projectPlanNodeModels);

        //void RemoveChildren(IEnumerable<int> managedPlans);

        //void ClearChildren();



        public void RemoveChildren(IEnumerable<Guid> managedPlans)
        {
            try
            {
                lock (m_Lock)
                {
                    m_Children.Edit(children =>
                    {

                        IEnumerable<IManagedPlanViewModel> projectPlans = [.. Children.Where(x => managedPlans.Contains(x.Id))];

                        foreach (IManagedPlanViewModel projectPlan in projectPlans)
                        {
                            projectPlan.Dispose();
                        }
                    });

                    //IsProjectUpdated = true;
                }
            }
            finally
            {
                //IsBusy = false;
            }
        }

        //public void UpdateManagedActivities(IEnumerable<UpdateDependentActivityModel> updateModels)
        //{
        //    try
        //    {
        //        lock (m_Lock)
        //        {
        //            m_Activities.Edit(activities =>
        //            {
        //                IsBusy = true;
        //                Dictionary<int, IManagedActivityViewModel> activityLookup = Activities.ToDictionary(x => x.Id);

        //                foreach (UpdateDependentActivityModel updateModel in updateModels)
        //                {
        //                    if (activityLookup.TryGetValue(updateModel.Id, out IManagedActivityViewModel? activity))
        //                    {
        //                        if (activity is IEditableObject editable)
        //                        {
        //                            editable.BeginEdit();

        //                            if (updateModel.IsNameEdited)
        //                            {
        //                                activity.Name = updateModel.Name;
        //                            }
        //                            if (updateModel.IsNotesEdited)
        //                            {
        //                                activity.Notes = updateModel.Notes;
        //                            }
        //                            if (updateModel.IsTargetWorkStreamsEdited)
        //                            {
        //                                activity.WorkStreamSelector.SetSelectedTargetWorkStreams([.. updateModel.TargetWorkStreams]);
        //                            }
        //                            if (updateModel.IsTargetResourcesEdited)
        //                            {
        //                                activity.ResourceSelector.SetSelectedTargetResources([.. updateModel.TargetResources]);
        //                            }
        //                            if (updateModel.IsTargetResourceOperatorEdited)
        //                            {
        //                                activity.TargetResourceOperator = updateModel.TargetResourceOperator;
        //                            }
        //                            if (updateModel.IsHasNoCostEdited)
        //                            {
        //                                activity.HasNoCost = updateModel.HasNoCost;
        //                            }
        //                            if (updateModel.IsHasNoBillingEdited)
        //                            {
        //                                activity.HasNoBilling = updateModel.HasNoBilling;
        //                            }
        //                            if (updateModel.IsHasNoEffortEdited)
        //                            {
        //                                activity.HasNoEffort = updateModel.HasNoEffort;
        //                            }
        //                            if (updateModel.IsHasNoRiskEdited)
        //                            {
        //                                activity.HasNoRisk = updateModel.HasNoRisk;
        //                            }

        //                            editable.EndEdit();
        //                        }
        //                    }
        //                }
        //            });

        //            IsProjectUpdated = true;
        //        }
        //    }
        //    finally
        //    {
        //        IsBusy = false;
        //    }
        //}



        public void ClearChildren()
        {
            try
            {
                lock (m_Lock)
                {
                    m_Children.Edit(children =>
                    {
                        //IsBusy = true;

                        foreach (IManagedPlanViewModel projectPlan in Children)
                        {
                            projectPlan.Dispose();
                        }
                        children.Clear();
                    });
                }
            }
            finally
            {
                //IsBusy = false;
            }
        }






















        //public bool CanBeRemoved => false;




        //public void SetAsReadOnly()
        //{

        //}

        //public void SetAsRemovable()
        //{

        //}

        //public object CloneObject()
        //{
        //    return null;
        //}

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
