using ReactiveUI;
using System.ComponentModel;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ManagedHolidayViewModel
        : ViewModelBase, IManagedHolidayViewModel, IEditableObject
    {
        #region Fields

        private readonly IHolidaySettingsManagerViewModel m_HolidaySettingsManagerViewModel;
        private readonly IDateTimeCalculator m_DateTimeCalculator;

        #endregion

        #region Ctors

        public ManagedHolidayViewModel(
            IHolidaySettingsManagerViewModel holidaySettingsManagerViewModel,
            HolidayModel holiday,
            IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(holidaySettingsManagerViewModel);
            ArgumentNullException.ThrowIfNull(holiday);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            m_HolidaySettingsManagerViewModel = holidaySettingsManagerViewModel;
            m_DateTimeCalculator = dateTimeCalculator;
            Id = holiday.Id;
            m_Name = holiday.Name;
            m_Notes = holiday.Notes;
            m_RecurrenceRule = RecurrencePatternHelper.ToRule(holiday.RecurrencePattern);
            m_StartDateTime = holiday.StartDateTime;
            m_IsEditMuted = false;
        }

        #endregion

        #region Properties

        #endregion

        #region Private Members

        #endregion

        #region IManagedHolidayViewModel Members

        public int Id { get; }

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

        private RecurrenceRuleModel? m_RecurrenceRule;
        public RecurrenceRuleModel? RecurrenceRule
        {
            get => m_RecurrenceRule;
            set
            {
                this.RaiseAndSetIfChanged(ref m_RecurrenceRule, value);
                this.RaisePropertyChanged(nameof(RecurrencePattern));
                this.RaisePropertyChanged(nameof(RecurrencePatternDisplay));
            }
        }

        private DateTimeOffset? m_StartDateTime;
        public DateTime? StartDateTime
        {
            get => m_StartDateTime?.DateTime;
            set
            {
                // Convert to local now using TimeProvider as we do not know
                // if the input is provided as just a datetime from XAML.
                DateTimeOffset? input = value is null ? null : m_DateTimeCalculator.GetLocalNow(value.Value);
                this.RaiseAndSetIfChanged(ref m_StartDateTime, input);
            }
        }

        public string RecurrencePattern
        {
            get
            {
                if (RecurrenceRule is null)
                {
                    return string.Empty;
                }
                return RecurrencePatternHelper.ToPattern(RecurrenceRule);
            }
        }

        public string RecurrencePatternDisplay
        {
            get
            {
                if (RecurrenceRule is null)
                {
                    return string.Empty;
                }
                return RecurrenceRuleHelper.ToPhrase(RecurrenceRule);
            }
        }

        public bool IsEditing => m_isDirty;

        public object CloneObject()
        {
            return new HolidayModel
            {
                Id = Id,
                Name = Name,
                Notes = Notes,
                StartDateTime = StartDateTime,
                RecurrencePattern = RecurrencePattern,
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

                if (!IsEditMuted)
                {
                    m_HolidaySettingsManagerViewModel.AreSettingsUpdated = true;
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
                // TODO: dispose managed state (managed objects).
                KillSubscriptions();
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
