using Prism.Commands;
using Prism.Interactivity.InteractionRequest;
using Prism.Mvvm;
using System;
using System.Windows.Input;

namespace Zametek.ViewModel.ProjectPlan
{
    public class BasicNotificationViewModel
        : BindableBase, IInteractionRequestAware
    {
        #region Fields

        private INotification m_Notification;

        #endregion

        #region Ctors

        public BasicNotificationViewModel()
        {
            ConfirmCommand = new DelegateCommand(Confirm);
        }

        #endregion

        #region Properties

        public object Content
        {
            get
            {
                return Notification?.Content;
            }
        }

        public Action OnClose
        {
            get;
            set;
        }

        #endregion

        #region Commands

        public ICommand ConfirmCommand
        {
            get;
            private set;
        }

        #endregion

        #region Public Methods

        public virtual void Confirm()
        {
            ConfirmInteraction?.Invoke();
            FinishInteraction?.Invoke();
            OnClose?.Invoke();
        }

        public Action ConfirmInteraction
        {
            get;
            set;
        }

        #endregion

        #region IInteractionRequestAware

        public Action FinishInteraction
        {
            get;
            set;
        }

        public virtual INotification Notification
        {
            get
            {
                return m_Notification;
            }
            set
            {
                m_Notification = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(Content));
            }
        }

        #endregion
    }
}
