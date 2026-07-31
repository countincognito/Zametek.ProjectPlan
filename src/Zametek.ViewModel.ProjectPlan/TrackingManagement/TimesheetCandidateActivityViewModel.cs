using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class TimesheetCandidateActivityViewModel
        : ITimesheetCandidateActivityViewModel
    {
        #region Ctors

        public TimesheetCandidateActivityViewModel(
            int id,
            string displayName)
        {
            ArgumentNullException.ThrowIfNull(displayName);
            Id = id;
            DisplayName = displayName;
        }

        #endregion

        #region ITimesheetCandidateActivityViewModel Members

        public int Id { get; }

        public string DisplayName { get; }

        #endregion
    }
}
