using Avalonia.Data;
using ReactiveUI;
using System.ComponentModel;
using System.Reactive.Linq;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ManagedResourceViewModel
        : ViewModelBase, IManagedResourceViewModel, IEditableObject
    {
        #region Fields

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IResourceSettingsManagerViewModel m_ResourceSettingsManagerViewModel;

        private readonly IDisposable? m_WorkStreamSettingsSub;

        #endregion

        #region Ctors

        public ManagedResourceViewModel(
            ICoreViewModel coreViewModel,
            IResourceSettingsManagerViewModel resourceSettingsManagerViewModel,
            ResourceModel resource)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(resourceSettingsManagerViewModel);
            ArgumentNullException.ThrowIfNull(resource);
            m_CoreViewModel = coreViewModel;
            m_ResourceSettingsManagerViewModel = resourceSettingsManagerViewModel;
            Id = resource.Id;
            m_Name = resource.Name;
            m_IsExplicitTarget = resource.IsExplicitTarget;
            m_IsInactive = resource.IsInactive;
            m_InterActivityAllocationType = resource.InterActivityAllocationType;
            m_UnitCost = resource.UnitCost;
            m_AllocationOrder = resource.AllocationOrder;
            m_DisplayOrder = resource.DisplayOrder;
            m_ColorFormat = resource.ColorFormat;

            m_TargetWorkStreams = new HashSet<int>(resource.InterActivityPhases);
            WorkStreamSelector = new WorkStreamSelectorViewModel(phaseOnly: true);
            m_WorkStreamSettings = m_CoreViewModel.WorkStreamSettings;
            RefreshWorkStreamSelector();

            TrackerSet = new ResourceTrackerSetViewModel(
                m_CoreViewModel, this, Id, resource.Trackers ?? []);

            m_InterActivityAllocationIsIndirect = this
                .WhenAnyValue(
                    core => core.InterActivityAllocationType,
                    (interActivityAllocationType) => interActivityAllocationType == InterActivityAllocationType.Indirect)
                .ToProperty(this, x => x.InterActivityAllocationIsIndirect);

            m_WorkStreamSettingsSub = this
                .WhenAnyValue(x => x.m_CoreViewModel.WorkStreamSettings)
                //.ObserveOn(RxApp.TaskpoolScheduler)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x => WorkStreamSettings = x);
        }

        #endregion

        #region Properties

        private readonly ObservableAsPropertyHelper<bool> m_InterActivityAllocationIsIndirect;
        public bool InterActivityAllocationIsIndirect => m_InterActivityAllocationIsIndirect.Value;

        private WorkStreamSettingsModel m_WorkStreamSettings;
        private WorkStreamSettingsModel WorkStreamSettings
        {
            get => m_WorkStreamSettings;
            set
            {
                m_WorkStreamSettings = value;
                SetNewTargetWorkStreams();
            }
        }

        #endregion

        private void UpdateActivityTargetWorkStreams()
        {
            m_TargetWorkStreams.Clear();
            m_TargetWorkStreams.UnionWith(WorkStreamSelector.SelectedWorkStreamIds);
            this.RaisePropertyChanged(nameof(InterActivityPhases));
            this.RaisePropertyChanged(nameof(WorkStreamSelector));
        }

        private void SetNewTargetWorkStreams()
        {
            UpdateActivityTargetWorkStreams();
            RefreshWorkStreamSelector();
            UpdateActivityTargetWorkStreams();
        }

        private void RefreshWorkStreamSelector()
        {
            var selectedTargetWorkStreams = new HashSet<int>(m_TargetWorkStreams);

            IEnumerable<TargetWorkStreamModel> targetWorkStreams = WorkStreamSettings
                .WorkStreams.Select(
                    x => new TargetWorkStreamModel
                    {
                        Id = x.Id,
                        Name = x.Name,
                        IsPhase = x.IsPhase,
                    });

            WorkStreamSelector.SetTargetWorkStreams(targetWorkStreams, selectedTargetWorkStreams);
        }

        #region IManagedResourceViewModel Members

        public int Id { get; }

        private string m_Name;
        public string Name
        {
            get => m_Name;
            set => this.RaiseAndSetIfChanged(ref m_Name, value);
        }

        private bool m_IsExplicitTarget;
        public bool IsExplicitTarget
        {
            get => m_IsExplicitTarget;
            set
            {
                if (m_IsExplicitTarget != value)
                {
                    BeginEdit();
                    m_IsExplicitTarget = value;
                    EndEdit();
                }
                this.RaisePropertyChanged();
            }
        }

        private bool m_IsInactive;
        public bool IsInactive
        {
            get => m_IsInactive;
            set
            {
                if (m_IsInactive != value)
                {
                    BeginEdit();
                    m_IsInactive = value;
                    EndEdit();
                }
                this.RaisePropertyChanged();
            }
        }

        private InterActivityAllocationType m_InterActivityAllocationType;
        public InterActivityAllocationType InterActivityAllocationType
        {
            get => m_InterActivityAllocationType;
            set => this.RaiseAndSetIfChanged(ref m_InterActivityAllocationType, value);
        }

        private readonly HashSet<int> m_TargetWorkStreams;
        public HashSet<int> InterActivityPhases => m_TargetWorkStreams;

        private double m_UnitCost;
        public double UnitCost
        {
            get => m_UnitCost;
            set
            {
                if (value < 0)
                {
                    throw new DataValidationException(Resource.ProjectPlan.Messages.Message_UnitCostMustBeGreaterThanZero);
                }
                this.RaiseAndSetIfChanged(ref m_UnitCost, value);
            }
        }

        private int m_DisplayOrder;
        public int DisplayOrder
        {
            get => m_DisplayOrder;
            set => this.RaiseAndSetIfChanged(ref m_DisplayOrder, value);
        }

        private int m_AllocationOrder;
        public int AllocationOrder
        {
            get => m_AllocationOrder;
            set => this.RaiseAndSetIfChanged(ref m_AllocationOrder, value);
        }

        private ColorFormatModel m_ColorFormat;
        public ColorFormatModel ColorFormat
        {
            get => m_ColorFormat;
            set
            {
                if (m_ColorFormat != value)
                {
                    BeginEdit();
                    m_ColorFormat = value;
                    EndEdit();
                }
                this.RaisePropertyChanged();
            }
        }

        public IWorkStreamSelectorViewModel WorkStreamSelector { get; }

        public IResourceTrackerSetViewModel TrackerSet { get; }

        public bool IsEditing => m_isDirty;

        public object CloneObject()
        {
            return new Resource<int, int>(Id, Name, IsExplicitTarget, IsInactive, InterActivityAllocationType, UnitCost, AllocationOrder, InterActivityPhases);
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
                UpdateActivityTargetWorkStreams();
                TrackerSet.RefreshIndex();
                m_ResourceSettingsManagerViewModel.AreSettingsUpdated = true;
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
            m_WorkStreamSettingsSub?.Dispose();
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
                TrackerSet.Dispose();
                m_InterActivityAllocationIsIndirect?.Dispose();
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
