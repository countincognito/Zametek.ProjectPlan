using ReactiveUI;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ActivityEditViewModel
        : ViewModelBase, IActivityEditViewModel
    {
        #region Ctors

        public ActivityEditViewModel(
            IEnumerable<ResourceModel> resources,
            IEnumerable<WorkStreamModel> workStreams)
        {
            ResourceSelector = new ResourceSelectorViewModel();
            IEnumerable<TargetResourceModel> targetResources = resources
                .Select(
                    x => new TargetResourceModel
                    {
                        Id = x.Id,
                        Name = x.Name,
                    });
            ResourceSelector.SetTargetResources(targetResources, []);

            WorkStreamSelector = new WorkStreamSelectorViewModel();
            IEnumerable<TargetWorkStreamModel> targetWorkStreams = workStreams
                .Select(
                    x => new TargetWorkStreamModel
                    {
                        Id = x.Id,
                        Name = x.Name,
                        IsPhase = x.IsPhase,
                    });
            WorkStreamSelector.SetTargetWorkStreams(targetWorkStreams, []);
        }

        #endregion

        #region Private Members

        #endregion

        #region IActivityManagerViewModel Members

        public IResourceSelectorViewModel ResourceSelector { get; }

        private bool m_IsResourceSelectorActive;
        public bool IsResourceSelectorActive
        {
            get => m_IsResourceSelectorActive;
            set
            {
                m_IsResourceSelectorActive = value;
                this.RaisePropertyChanged();
            }
        }

        public IWorkStreamSelectorViewModel WorkStreamSelector { get; }

        private bool m_IsWorkStreamSelectorActive;
        public bool IsWorkStreamSelectorActive
        {
            get => m_IsWorkStreamSelectorActive;
            set
            {
                m_IsWorkStreamSelectorActive = value;
                this.RaisePropertyChanged();
            }
        }

        private bool m_HasNoCost;
        public bool HasNoCost
        {
            get => m_HasNoCost;
            set
            {
                m_HasNoCost = value;
                this.RaisePropertyChanged();
            }
        }

        private bool m_IsHasNoCostActive;
        public bool IsHasNoCostActive
        {
            get => m_IsHasNoCostActive;
            set
            {
                m_IsHasNoCostActive = value;
                this.RaisePropertyChanged();
            }
        }

        private LogicalOperator m_TargetResourceOperator;
        public LogicalOperator TargetResourceOperator
        {
            get => m_TargetResourceOperator;
            set
            {
                m_TargetResourceOperator = value;
                this.RaisePropertyChanged();
            }
        }

        private bool m_IsTargetResourceOperatorActive;
        public bool IsTargetResourceOperatorActive
        {
            get => m_IsTargetResourceOperatorActive;
            set
            {
                m_IsTargetResourceOperatorActive = value;
                this.RaisePropertyChanged();
            }
        }

        public UpdateDependentActivityModel BuildUpdateModel()
        {
            var updateModel = new UpdateDependentActivityModel
            {
                Name = string.Empty,
                IsNameEdited = false,

                Notes = string.Empty,
                IsNotesEdited = false,

                IsTargetWorkStreamsEdited = IsWorkStreamSelectorActive,

                IsTargetResourcesEdited = IsResourceSelectorActive,

                HasNoCost = HasNoCost,
                IsHasNoCostEdited = IsHasNoCostActive,

                TargetResourceOperator = TargetResourceOperator,
                IsTargetResourceOperatorEdited = IsTargetResourceOperatorActive,
            };
            updateModel.TargetResources.AddRange(ResourceSelector.SelectedResourceIds);
            updateModel.TargetWorkStreams.AddRange(WorkStreamSelector.SelectedWorkStreamIds);
            return updateModel;
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
                // Dispose managed state (managed objects).
                //m_IsBusy?.Dispose();
            }

            // Free unmanaged resources (unmanaged objects) and override a finalizer below.
            // Set large fields to null.

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
