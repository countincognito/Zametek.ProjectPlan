using ReactiveUI;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ResourceEditViewModel
        : ViewModelBase, IResourceEditViewModel
    {
        #region Ctors

        public ResourceEditViewModel(IEnumerable<WorkStreamModel> workStreams)
        {
            WorkStreamSelector = new WorkStreamSelectorViewModel(phaseOnly: true);
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

        #region IResourceManagerViewModel Members

        private bool m_IsExplicitTarget;
        public bool IsExplicitTarget
        {
            get => m_IsExplicitTarget;
            set
            {
                m_IsExplicitTarget = value;
                this.RaisePropertyChanged();
            }
        }

        private bool m_IsIsExplicitTargetActive;
        public bool IsIsExplicitTargetActive
        {
            get => m_IsIsExplicitTargetActive;
            set
            {
                m_IsIsExplicitTargetActive = value;
                this.RaisePropertyChanged();
            }
        }

        private bool m_IsInactive;
        public bool IsInactive
        {
            get => m_IsInactive;
            set
            {
                m_IsInactive = value;
                this.RaisePropertyChanged();
            }
        }

        private bool m_IsIsInactiveActive;
        public bool IsIsInactiveActive
        {
            get => m_IsIsInactiveActive;
            set
            {
                m_IsIsInactiveActive = value;
                this.RaisePropertyChanged();
            }
        }

        private InterActivityAllocationType m_InterActivityAllocationType;
        public InterActivityAllocationType InterActivityAllocationType
        {
            get => m_InterActivityAllocationType;
            set
            {
                m_InterActivityAllocationType = value;
                this.RaisePropertyChanged();
            }
        }

        private bool m_IsInterActivityAllocationTypeActive;
        public bool IsInterActivityAllocationTypeActive
        {
            get => m_IsInterActivityAllocationTypeActive;
            set
            {
                m_IsInterActivityAllocationTypeActive = value;
                this.RaisePropertyChanged();
            }
        }

        private double m_UnitCost;
        public double UnitCost
        {
            get => m_UnitCost;
            set
            {
                m_UnitCost = value;
                this.RaisePropertyChanged();
            }
        }

        private bool m_IsUnitCostActive;
        public bool IsUnitCostActive
        {
            get => m_IsUnitCostActive;
            set
            {
                m_IsUnitCostActive = value;
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

        public UpdateResourceModel BuildUpdateModel()
        {
            var updateModel = new UpdateResourceModel
            {
                Name = string.Empty,
                IsNameEdited = false,

                IsExplicitTarget = IsExplicitTarget,
                IsIsExplicitTargetEdited = IsIsExplicitTargetActive,

                IsInactive = IsInactive,
                IsIsInactiveEdited = IsIsInactiveActive,

                InterActivityAllocationType = InterActivityAllocationType,
                IsInterActivityAllocationTypeEdited = IsInterActivityAllocationTypeActive,

                UnitCost = UnitCost,
                IsUnitCostEdited = IsUnitCostActive,

                IsInterActivityPhasesEdited = IsWorkStreamSelectorActive,
            };
            updateModel.InterActivityPhases.AddRange(WorkStreamSelector.SelectedWorkStreamIds);
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
