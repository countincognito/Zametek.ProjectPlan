using ReactiveUI;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class WorkStreamEditViewModel
        : ViewModelBase
    {
        #region Ctors

        public WorkStreamEditViewModel()
        {
            m_ColorFormat = new ColorFormatModel();
        }

        #endregion

        #region Public Members

        private bool m_IsPhase;
        public bool IsPhase
        {
            get => m_IsPhase;
            set
            {
                m_IsPhase = value;
                this.RaisePropertyChanged();
            }
        }

        private bool m_IsIsPhaseActive;
        public bool IsIsPhaseActive
        {
            get => m_IsIsPhaseActive;
            set
            {
                m_IsIsPhaseActive = value;
                this.RaisePropertyChanged();
            }
        }

        private ColorFormatModel m_ColorFormat;
        public ColorFormatModel ColorFormat
        {
            get => m_ColorFormat;
            set
            {
                m_ColorFormat = value;
                this.RaisePropertyChanged();
            }
        }

        private bool m_IsColorFormatActive;
        public bool IsColorFormatActive
        {
            get => m_IsColorFormatActive;
            set
            {
                m_IsColorFormatActive = value;
                this.RaisePropertyChanged();
            }
        }

        public (bool IsPhase, bool IsIsPhaseEdited, ColorFormatModel ColorFormat, bool IsColorFormatActive) BuildUpdateValues()
        {
            return (IsPhase, IsIsPhaseActive, ColorFormat, IsColorFormatActive);
        }

        #endregion
    }
}
