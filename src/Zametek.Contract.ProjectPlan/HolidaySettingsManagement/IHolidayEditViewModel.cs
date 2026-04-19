using System.ComponentModel;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IHolidayEditViewModel
        : IDisposable, INotifyPropertyChanged, IKillSubscriptions
    {
        DateTime? StartDateTime { get; set; }

        RecurrenceFrequency RecurrenceFrequency { get; set; }

        int Interval { get; set; }

        bool IsEndNever { get; set; }

        bool IsEndUntil { get; set; }

        bool IsEndCount { get; set; }

        DateTime? Until { get; set; }

        int? Count { get; set; }

        bool ByWeekDaysMonday { get; set; }
        bool ByWeekDaysTuesday { get; set; }
        bool ByWeekDaysWednesday { get; set; }
        bool ByWeekDaysThursday { get; set; }
        bool ByWeekDaysFriday { get; set; }
        bool ByWeekDaysSaturday { get; set; }
        bool ByWeekDaysSunday { get; set; }

        bool IsMonthDay { get; set; }

        int? ByMonthDay { get; set; }

        bool IsMonthWeekday { get; set; }

        SetByIntModel? ByMonthSetPosSelection { get; set; }
        List<SetByIntModel> ByMonthSetPosOptions { get; }

        SetByStringModel? ByMonthWeekdaySelection { get; set; }
        List<SetByStringModel> ByMonthWeekdayOptions { get; }

        bool IsYearlyMonthDay { get; set; }

        int? YearlyDayOfMonth { get; set; }

        bool IsYearlyMonthWeekday { get; set; }

        SetByIntModel? YearlySetPosSelection { get; set; }
        List<SetByIntModel> YearlySetPosOptions { get; }

        SetByStringModel? YearlyWeekdaySelection { get; set; }
        List<SetByStringModel> YearlyWeekdayOptions { get; }

        SetByIntModel? YearlyMonthSelection { get; set; }
        List<SetByIntModel> YearlyMonthOptions { get; }

        RecurrenceRuleModel RecurrenceRule { get; }

        string RecurrencePattern { get; }

        string RecurrencePatternDisplay { get; }

        ICommand ChangeRecurrenceFrequencyCommand { get; }
    }
}
