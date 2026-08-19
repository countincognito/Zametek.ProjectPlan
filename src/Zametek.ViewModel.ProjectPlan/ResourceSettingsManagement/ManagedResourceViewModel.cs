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
            m_Notes = resource.Notes;
            m_IsExplicitTarget = resource.IsExplicitTarget;
            m_IsInactive = resource.IsInactive;
            m_ActivityAllocationType = resource.ActivityAllocationType;
            m_InterActivityAllocationType = resource.InterActivityAllocationType;
            m_UnitCost = resource.UnitCost;
            m_UnitBilling = resource.UnitBilling;
            m_FixedCost = resource.FixedCost;
            m_FixedBilling = resource.FixedBilling;
            m_AllocationOrder = resource.AllocationOrder;
            m_DisplayOrder = resource.DisplayOrder;
            m_ColorFormat = resource.ColorFormat;
            m_IsEditMuted = false;

            m_TargetWorkStreams = [.. resource.InterActivityPhases];
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

            // Work stream settings are not observed here. The settings manager pushes them
            // in synchronously through SetWorkStreamSettings when they change, in the same
            // way the core view model pushes them into the activities: a callback deferred
            // to another thread would rewrite this resource's target phases at a moment of
            // the scheduler's choosing, which is the shape of mutation that corrupted a
            // compilation's input (ARCHITECTURE section 7 rule 10). Nothing here reaches a
            // compiler, but the rule is worth keeping uniform, and the push also settles
            // the phases before the recompilation the change triggers.
            m_HasPhases = this
                .WhenAnyValue(x => x.m_CoreViewModel.HasPhases)
                .ToProperty(this, x => x.HasPhases);
        }

        #endregion

        #region Properties

        private readonly ObservableAsPropertyHelper<bool> m_InterActivityAllocationIsIndirect;
        public bool InterActivityAllocationIsIndirect => m_InterActivityAllocationIsIndirect.Value;

        private WorkStreamSettingsModel m_WorkStreamSettings;

        #endregion

        #region Private Members

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

            IEnumerable<TargetWorkStreamModel> targetWorkStreams = m_WorkStreamSettings
                .WorkStreams.Select(
                    x => new TargetWorkStreamModel
                    {
                        Id = x.Id,
                        Name = x.Name,
                        IsPhase = x.IsPhase,
                    });

            WorkStreamSelector.SetTargetWorkStreams(targetWorkStreams, selectedTargetWorkStreams);
        }

        #endregion

        #region IManagedResourceViewModel Members

        public int Id { get; }

        private int m_DisplayOrder;
        public int DisplayOrder
        {
            get => m_DisplayOrder;
            set => this.RaiseAndSetIfChanged(ref m_DisplayOrder, value);
        }

        private string m_Name;
        public string Name
        {
            get => m_Name;
            set => this.RaiseAndSetIfChanged(ref m_Name, value);
        }

        private string m_Notes;
        public string Notes
        {
            get => m_Notes;
            set => this.RaiseAndSetIfChanged(ref m_Notes, value);
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

        private ActivityAllocationType m_ActivityAllocationType;
        public ActivityAllocationType ActivityAllocationType
        {
            get => m_ActivityAllocationType;
            set => this.RaiseAndSetIfChanged(ref m_ActivityAllocationType, value);
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
                    throw new DataValidationException(Resource.ProjectPlan.Messages.Message_UnitCostMustBeZeroOrGreater);
                }
                this.RaiseAndSetIfChanged(ref m_UnitCost, value);
            }
        }

        private double m_UnitBilling;
        public double UnitBilling
        {
            get => m_UnitBilling;
            set
            {
                if (value < 0)
                {
                    throw new DataValidationException(Resource.ProjectPlan.Messages.Message_UnitBillingMustBeZeroOrGreater);
                }
                this.RaiseAndSetIfChanged(ref m_UnitBilling, value);
            }
        }

        private double m_FixedCost;
        public double FixedCost
        {
            get => m_FixedCost;
            set
            {
                if (value < 0)
                {
                    throw new DataValidationException(Resource.ProjectPlan.Messages.Message_FixedCostMustBeZeroOrGreater);
                }
                this.RaiseAndSetIfChanged(ref m_FixedCost, value);
            }
        }

        private double m_FixedBilling;
        public double FixedBilling
        {
            get => m_FixedBilling;
            set
            {
                if (value < 0)
                {
                    throw new DataValidationException(Resource.ProjectPlan.Messages.Message_FixedBillingMustBeZeroOrGreater);
                }
                this.RaiseAndSetIfChanged(ref m_FixedBilling, value);
            }
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

        private readonly ObservableAsPropertyHelper<bool> m_HasPhases;
        public bool HasPhases => m_HasPhases.Value;

        public IResourceTrackerSetViewModel TrackerSet { get; }

        public bool IsEditing => m_isDirty;

        /// <summary>
        /// Absorbs new work stream settings: stores them, rebuilds the work stream
        /// selector, and reconciles this resource's target phases against the work streams
        /// that now exist. Invoked synchronously by the settings manager when the settings
        /// change - see the note in the constructor for why it is not observed instead.
        /// </summary>
        public void SetWorkStreamSettings(WorkStreamSettingsModel workStreamSettings)
        {
            ArgumentNullException.ThrowIfNull(workStreamSettings);
            m_WorkStreamSettings = workStreamSettings;
            SetNewTargetWorkStreams();
        }

        public ResourceModel DeepCopy()
        {
            return new()
            {
                Id = Id,
                DisplayOrder = DisplayOrder,
                Name = Name,
                Notes = Notes,
                IsExplicitTarget = IsExplicitTarget,
                IsInactive = IsInactive,
                ActivityAllocationType = ActivityAllocationType,
                InterActivityAllocationType = InterActivityAllocationType,
                InterActivityPhases = [.. InterActivityPhases],
                UnitCost = UnitCost,
                UnitBilling = UnitBilling,
                FixedCost = FixedCost,
                FixedBilling = FixedBilling,
                AllocationOrder = AllocationOrder,
                ColorFormat = ColorFormat,
                Trackers = TrackerSet.CloneTrackers(),
            };
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

                if (!IsEditMuted)
                {
                    m_ResourceSettingsManagerViewModel.AreSettingsUpdated = true;
                }
            }
        }

        public void CancelEdit()
        {
            m_isDirty = false;
        }

        #endregion

        #region IMuteEdits Members

        private bool m_IsEditMuted;
        public bool IsEditMuted
        {
            get => m_IsEditMuted;
            set => this.RaiseAndSetIfChanged(ref m_IsEditMuted, value);
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
                KillSubscriptions();
                m_HasPhases?.Dispose();
                TrackerSet.Dispose();
                m_InterActivityAllocationIsIndirect?.Dispose();
            }

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
