using Prism.Interactivity.InteractionRequest;
using System.Collections.ObjectModel;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class ArrowGraphSettingsManagerViewModel
        : BasicConfirmationViewModel, IArrowGraphSettingsManagerViewModel
    {
        #region Ctors

        public ArrowGraphSettingsManagerViewModel()
            : base()
        { }

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

        public ObservableCollection<ManagedActivitySeverityViewModel> ActivitySeverities
        {
            get
            {
                return ((ArrowGraphSettingsManagerConfirmation)Notification).ActivitySeverities;
            }
        }

        #endregion
    }
}
