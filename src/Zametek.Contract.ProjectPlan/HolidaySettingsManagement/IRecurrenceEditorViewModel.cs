using System.ComponentModel;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IRecurrenceEditorViewModel
        : IDisposable, INotifyPropertyChanged, IKillSubscriptions
    {
        DateTime? StartDateTime { get; set; }

        RecurrenceFrequencyType RecurrenceFrequency { get; set; }

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

        bool IsMonthWeekday { get; set; }

        int? ByMonthDay { get; set; }

        int? BySetPosSelection { get; set; }

        string? ByMonthWeekdaySelection { get; set; }

        int YearlyMonthIndex { get; set; }

        int YearlyDayOfMonth { get; set; }

        int? YearlySetPosSelection { get; set; }

        string? YearlyWeekdaySelection { get; set; }

        int YearlyPatternMonthIndex { get; set; }

        string RRuleString { get; }

        ICommand ChangeRecurrenceFrequencyCommand { get; }

        void LoadFromPattern(string rrule);
    }
}
