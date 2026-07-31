using ReactiveUI;
using System.Globalization;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class TimesheetDayTotalViewModel
        : ViewModelBase, ITimesheetDayTotalViewModel
    {
        #region Ctors

        public TimesheetDayTotalViewModel(int dayOffset)
        {
            DayOffset = dayOffset;
        }

        #endregion

        #region ITimesheetDayTotalViewModel Members

        public int DayOffset { get; }

        private int? m_Total;
        public int? Total => m_Total;

        public TimesheetDayLoad Load => TimesheetHelper.Classify(m_Total);

        public string TotalDisplay => m_Total?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;

        #endregion

        #region Public Members

        public void SetTotal(int? total)
        {
            if (m_Total != total)
            {
                m_Total = total;
                this.RaisePropertyChanged(nameof(Total));
                this.RaisePropertyChanged(nameof(Load));
                this.RaisePropertyChanged(nameof(TotalDisplay));
            }
        }

        #endregion
    }
}
