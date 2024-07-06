using ReactiveUI;
using System.ComponentModel;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ManagedWorkStreamViewModel
        : ViewModelBase, IManagedWorkStreamViewModel, IEditableObject
    {
        #region Fields

        private readonly IWorkStreamSettingsManagerViewModel m_WorkStreamSettingsManagerViewModel;

        #endregion

        #region Ctors

        public ManagedWorkStreamViewModel(
            IWorkStreamSettingsManagerViewModel workStreamSettingsManagerViewModel,
            WorkStreamModel workStream)
        {
            ArgumentNullException.ThrowIfNull(workStreamSettingsManagerViewModel);
            ArgumentNullException.ThrowIfNull(workStream);
            m_WorkStreamSettingsManagerViewModel = workStreamSettingsManagerViewModel;
            Id = workStream.Id;
            m_Name = workStream.Name;
            m_IsPhase = workStream.IsPhase;
            m_DisplayOrder = workStream.DisplayOrder;
            m_ColorFormat = workStream.ColorFormat;
        }

        #endregion

        #region IManagedWorkStreamViewModel Members

        public int Id { get; }

        private string m_Name;
        public string Name
        {
            get => m_Name;
            set => this.RaiseAndSetIfChanged(ref m_Name, value);
        }

        private bool m_IsPhase;
        public bool IsPhase
        {
            get => m_IsPhase;
            set
            {
                if (m_IsPhase != value)
                {
                    BeginEdit();
                    m_IsPhase = value;
                    EndEdit();
                }
                this.RaisePropertyChanged();
            }
        }

        private int m_DisplayOrder;
        public int DisplayOrder
        {
            get => m_DisplayOrder;
            set => this.RaiseAndSetIfChanged(ref m_DisplayOrder, value);
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
            return new WorkStreamModel
            {
                Id = Id,
                Name = Name,
                IsPhase = IsPhase,
                DisplayOrder = DisplayOrder,
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
                m_WorkStreamSettingsManagerViewModel.AreSettingsUpdated = true;
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
