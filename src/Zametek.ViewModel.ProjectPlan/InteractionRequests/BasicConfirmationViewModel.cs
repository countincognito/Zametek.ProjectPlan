using Prism.Commands;
using Prism.Interactivity.InteractionRequest;
using System;
using System.Windows.Input;

namespace Zametek.ViewModel.ProjectPlan
{
    public class BasicConfirmationViewModel
        : BasicNotificationViewModel
    {
        #region Fields

        private IConfirmation m_Confirmation;

        #endregion

        #region Ctors

        public BasicConfirmationViewModel()
        {
            CancelCommand = new DelegateCommand(Cancel);
        }

        #endregion

        #region Commands

        public ICommand CancelCommand
        {
            get;
            private set;
        }

        #endregion

        #region Public Methods

        public virtual void Cancel()
        {
            ConfirmInteraction?.Invoke();
            if (m_Confirmation != null)
            {
                m_Confirmation.Confirmed = false;
            }
            FinishInteraction?.Invoke();
            OnClose?.Invoke();
        }

        public Action CancelInteraction
        {
            get;
            set;
        }

        #endregion

        #region Overrides

        public override void Confirm()
        {
            ConfirmInteraction?.Invoke();
            if (m_Confirmation != null)
            {
                m_Confirmation.Confirmed = true;
            }
            FinishInteraction?.Invoke();
            OnClose?.Invoke();
        }

        public override INotification Notification
        {
            get
            {
                return m_Confirmation;
            }
            set
            {
                m_Confirmation = value as IConfirmation;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(Content));
            }
        }

        #endregion
    }
}
