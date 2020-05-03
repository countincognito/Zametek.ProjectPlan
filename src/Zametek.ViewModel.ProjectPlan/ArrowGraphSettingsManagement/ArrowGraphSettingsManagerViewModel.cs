using Prism.Interactivity.InteractionRequest;
using System.Collections.ObjectModel;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ArrowGraphSettingsManagerViewModel
        : BasicConfirmationViewModel, IArrowGraphSettingsManagerViewModel
    {
        #region Ctors

        public ArrowGraphSettingsManagerViewModel()
            : base()
        {
        }

        #endregion

        #region Overrides

        public override INotification Notification
        {
            get
            {
                return base.Notification;
            }
            set
            {
                base.Notification = value;
                RaisePropertyChanged(nameof(ActivitySeverities));
            }
        }

        #endregion

        #region IArrowGraphSettingsManagerViewModel Members

        public ObservableCollection<IManagedActivitySeverityViewModel> ActivitySeverities
        {
            get
            {
                return ((ArrowGraphSettingsManagerConfirmation)Notification).ActivitySeverities;
            }
        }

        #endregion
    }
}
