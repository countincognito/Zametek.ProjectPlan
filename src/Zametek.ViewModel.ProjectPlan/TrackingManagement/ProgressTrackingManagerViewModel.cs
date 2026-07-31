using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    // A dock tab can only be occupied by one dockable, so each tracking tab
    // gets a thin dockable host while all tracking state and behavior stay
    // in the shared ITrackingManagerViewModel.
    public class ProgressTrackingManagerViewModel
        : TrackingManagerViewModel, IProgressTrackingManagerViewModel
    {
        #region Ctors

        public ProgressTrackingManagerViewModel(
            ICoreViewModel coreViewModel,
            IResourceSettingsManagerViewModel resourceSettingsManagerViewModel,
            IDateTimeCalculator dateTimeCalculator)
            : base(coreViewModel, resourceSettingsManagerViewModel, dateTimeCalculator)
        {
            Id = Resource.ProjectPlan.Titles.Title_ProgressTrackingView;
            Title = Resource.ProjectPlan.Titles.Title_ProgressTrackingView;
        }

        #endregion
    }
}
