using Avalonia.Data;
using ReactiveUI;
using System.ComponentModel;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ManagedActivitySeverityViewModel
        : ViewModelBase, IManagedActivitySeverityViewModel, IEditableObject
    {
        #region Fields

        private readonly IArrowGraphSettingsManagerViewModel m_ArrowGraphSettingsManagerViewModel;

        #endregion

        #region Ctors

        public ManagedActivitySeverityViewModel(
            IArrowGraphSettingsManagerViewModel arrowGraphSettingsManagerViewModel,
            Guid id,
            ActivitySeverityModel activitySeverity)
        {
            ArgumentNullException.ThrowIfNull(arrowGraphSettingsManagerViewModel);
            ArgumentNullException.ThrowIfNull(activitySeverity);
            m_ArrowGraphSettingsManagerViewModel = arrowGraphSettingsManagerViewModel;
            Id = id;
            m_SlackLimit = activitySeverity.SlackLimit;
            m_CriticalityWeight = activitySeverity.CriticalityWeight;
            m_FibonacciWeight = activitySeverity.FibonacciWeight;
            m_ColorFormat = activitySeverity.ColorFormat;
        }

        #endregion

        #region IManagedActivitySeverityViewModel Members

        public Guid Id { get; }

        private int m_SlackLimit;
        public int SlackLimit
        {
            get => m_SlackLimit;
            set
            {
                if (value < 0)
                {
                    throw new DataValidationException(Resource.ProjectPlan.Messages.Message_SlackLimitMustBeEqualOrGreaterThanZero);
                }
                this.RaiseAndSetIfChanged(ref m_SlackLimit, value);
            }
        }

        private double m_CriticalityWeight;
        public double CriticalityWeight
        {
            get => m_CriticalityWeight;
            set
            {
                if (value < 0)
                {
                    throw new DataValidationException(Resource.ProjectPlan.Messages.Message_CriticalityWeightMustBeEqualOrGreaterThanZero);
                }
                this.RaiseAndSetIfChanged(ref m_CriticalityWeight, value);
            }
        }

        private double m_FibonacciWeight;
        public double FibonacciWeight
        {
            get => m_FibonacciWeight;
            set
            {
                if (value < 0)
                {
                    throw new DataValidationException(Resource.ProjectPlan.Messages.Message_FibonacciWeightMustBeEqualOrGreaterThanZero);
                }
                this.RaiseAndSetIfChanged(ref m_FibonacciWeight, value);
            }
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

        public object CloneObject()
        {
            return new ActivitySeverityModel
            {
                 SlackLimit = SlackLimit,
                 CriticalityWeight = CriticalityWeight,
                 FibonacciWeight = FibonacciWeight,
                 ColorFormat = ColorFormat,
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
                m_ArrowGraphSettingsManagerViewModel.AreSettingsUpdated = true;
            }
        }

        public void CancelEdit()
        {
            m_isDirty = false;
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
                //m_ProjectStartSub?.Dispose();
                //m_ResourceSettingsSub?.Dispose();
                //m_DateTimeCalculatorSub?.Dispose();
                //m_CompilationSub?.Dispose();
                //ResourceSelector.Dispose();
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
