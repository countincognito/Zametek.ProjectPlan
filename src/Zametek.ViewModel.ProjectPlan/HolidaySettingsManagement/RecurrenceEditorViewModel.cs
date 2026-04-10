using Ical.Net;
using Ical.Net.DataTypes;
using ReactiveUI;
using System.Reactive.Linq;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class RecurrenceEditorViewModel
        : ViewModelBase
    {

        #region Ctors

        public RecurrenceEditorViewModel()
        {
            m_StartDateTime = null;
            m_RecurrenceFrequency = RecurrenceFrequencyType.None;








            m_Interval = 1;
            m_IsEndNever = true;
            m_IsEndUntil = false;
            m_IsEndCount = false;
            m_Until = null;
            m_Count = null;

            // Weekly flags: Monday..Sunday
            ByWeekDays = [.. Enumerable.Repeat(false, 7)];

            // Month-day display 1..31
            //ByMonthDaysDisplay = [.. Enumerable.Range(1, 31)];
            //ByMonthDaysSelection = ByMonthDaysDisplay.ToDictionary(d => d, _ => false);

            m_ByMonthDay = 1;




            m_IsMonthDay = true;
            m_IsMonthWeekday = false;





            m_YearlyMonthIndex = 0;
            m_YearlyDayOfMonth = 1;

            m_YearlyPatternMonthIndex = 0;

            m_RRuleString = string.Empty;

            ChangeRecurrenceFrequencyCommand = ReactiveCommand.Create<RecurrenceFrequencyType>(ChangeRecurrenceFrequency);





            // Compute DetailsTemplateKey from Frequency
            this
                .WhenAnyValue(
                    x => x.StartDateTime,
                    x => x.RecurrenceFrequency,
                    x => x.Interval,
                    x => x.IsEndNever,
                    x => x.IsEndUntil,
                    x => x.IsEndCount,
                    x => x.Until,
                    x => x.Count,
                    x => x.IsMonthDay,
                    x => x.IsMonthWeekday,
                    x => x.ByMonthDay,
                    x => x.BySetPosSelection,
                    //x => x.ByMonthWeekdaySelection,
                    //x => x.YearlyMonthIndex,
                    //x => x.YearlyDayOfMonth,
                    //x => x.YearlySetPosSelection,
                    //x => x.YearlyWeekdaySelection,
                    //x => x.YearlyPatternMonthIndex,
                    (a, _, _, _, _, _, _, _, _, _, _, _) => a)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(freq =>
                {
                    RebuildRecurrencePattern();
                });





        }

        #endregion










        #region Private Members







        void RebuildRecurrencePattern()
        {
            if (RecurrenceFrequency == RecurrenceFrequencyType.None)
            {
                RecurrencePattern = null;
                RRuleString = string.Empty;
                return;
            }

            var freq = FrequencyTypeToIcal(RecurrenceFrequency);

            var pattern = new RecurrencePattern(freq)
            {
                Interval = Interval <= 0 ? 1 : Interval
            };

            // End conditions
            if (IsEndUntil && Until.HasValue)
            {
                pattern.Until = new CalDateTime(Until.Value, false);
                pattern.Count = 0;
            }
            else if (IsEndCount && Count.HasValue)
            {
                pattern.Count = Count.Value;
                pattern.Until = null;
            }
            else
            {
                pattern.Count = 0;
                pattern.Until = null;
            }

            //// WKST
            //if (!string.IsNullOrWhiteSpace(WeekStart))
            //{
            //    pattern.WeekStart = ParseWeekDay(WeekStart);
            //}

            // BYDAY for weekly
            if (RecurrenceFrequency == RecurrenceFrequencyType.Weekly)
            {
                pattern.ByDay.Clear();
                var codes = new[] { "MO", "TU", "WE", "TH", "FR", "SA", "SU" };
                for (int i = 0; i < codes.Length; i++)
                {
                    if (ByWeekDays[i])
                    {
                        pattern.ByDay.Add(new WeekDay(ParseWeekDay(codes[i])));
                    }
                }
            }

            // MONTHLY
            if (RecurrenceFrequency == RecurrenceFrequencyType.Monthly)
            {
                pattern.ByMonthDay.Clear();
                pattern.ByDay.Clear();
                pattern.BySetPosition.Clear();

                // ByMonthDay from selection
                //var selectedMonthDays = ByMonthDaysSelection
                //    .Where(kv => kv.Value)
                //    .Select(kv => kv.Key)
                //    .ToList();
                //foreach (var d in selectedMonthDays)
                //{
                //    pattern.ByMonthDay.Add(d);
                //}


                if (ByMonthDay.HasValue)
                    pattern.ByMonthDay.Add(ByMonthDay.GetValueOrDefault());







                // Pattern via BySetPosSelection + ByMonthWeekdaySelection
                if (BySetPosSelection.HasValue && !string.IsNullOrEmpty(ByMonthWeekdaySelection))
                {
                    pattern.BySetPosition.Add(BySetPosSelection.Value);

                    var tokens = ByMonthWeekdaySelection.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var t in tokens)
                    {
                        var wd = ParseWeekDay(t.Trim());
                        pattern.ByDay.Add(new WeekDay(wd));
                    }
                }
            }

            // YEARLY
            if (RecurrenceFrequency == RecurrenceFrequencyType.Yearly)
            {
                pattern.ByMonth.Clear();
                pattern.ByMonthDay.Clear();
                pattern.ByDay.Clear();
                pattern.BySetPosition.Clear();

                // Specific date
                if (YearlyMonthIndex >= 0 && YearlyMonthIndex <= 11 && YearlyDayOfMonth > 0)
                {
                    pattern.ByMonth.Add(YearlyMonthIndex + 1);
                    pattern.ByMonthDay.Add(YearlyDayOfMonth);
                }

                // Pattern tab
                if (YearlyPatternMonthIndex >= 0 && YearlyPatternMonthIndex <= 11
                    && YearlySetPosSelection.HasValue
                    && !string.IsNullOrEmpty(YearlyWeekdaySelection))
                {
                    pattern.ByMonth.Clear();
                    pattern.ByMonth.Add(YearlyPatternMonthIndex + 1);

                    pattern.BySetPosition.Add(YearlySetPosSelection.Value);

                    var tokens = YearlyWeekdaySelection.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var t in tokens)
                    {
                        var wd = ParseWeekDay(t.Trim());
                        pattern.ByDay.Add(new WeekDay(wd));
                    }
                }
            }

            // Other BY* fields
            CopyList(pattern.ByHour, ByHours);
            CopyList(pattern.ByMinute, ByMinutes);
            CopyList(pattern.BySecond, BySeconds);
            CopyList(pattern.ByYearDay, ByYearDays);
            CopyList(pattern.ByWeekNo, ByWeekNumbers);
            CopyList(pattern.BySetPosition, BySetPositions);

            // BYMONTH (explicit)
            pattern.ByMonth.Clear();
            foreach (var m in ByMonths.Distinct())
            {
                pattern.ByMonth.Add(m);
            }

            // RSCALE/SKIP extensions are not directly supported by iCal.NET RecurrencePattern,
            // but you can inject them into the ToString output if needed.
            RecurrencePattern = pattern;
            RRuleString = pattern.ToString();

            //if (!string.IsNullOrEmpty(RScale))
            //{
            //    RRuleString += $";RSCALE={RScale}";
            //}
            //if (!string.IsNullOrEmpty(Skip))
            //{
            //    RRuleString += $";SKIP={Skip}";
            //}
        }

        static void CopyList(IList<int> target, IList<int> source)
        {
            target.Clear();
            foreach (var v in source.Distinct())
                target.Add(v);
        }


        static FrequencyType FrequencyTypeToIcal(RecurrenceFrequencyType freq)
        {
            return freq switch
            {
                //FrequencyType.Secondly => RecurrenceFrequencyType.Secondly,
                //FrequencyType.Minutely => RecurrenceFrequencyType.Minutely,
                //FrequencyType.Hourly => RecurrenceFrequencyType.Hourly,
                RecurrenceFrequencyType.Daily => FrequencyType.Daily,
                RecurrenceFrequencyType.Weekly => FrequencyType.Weekly,
                RecurrenceFrequencyType.Monthly => FrequencyType.Monthly,
                RecurrenceFrequencyType.Yearly => FrequencyType.Yearly,
                _ => throw new ArgumentOutOfRangeException(nameof(freq)),
            };
        }

        static DayOfWeek ParseWeekDay(string code)
        {
            return code.ToUpperInvariant() switch
            {
                "MO" => DayOfWeek.Monday,
                "TU" => DayOfWeek.Tuesday,
                "WE" => DayOfWeek.Wednesday,
                "TH" => DayOfWeek.Thursday,
                "FR" => DayOfWeek.Friday,
                "SA" => DayOfWeek.Saturday,
                "SU" => DayOfWeek.Sunday,
                _ => DayOfWeek.Monday
            };
        }


















        private void ChangeRecurrenceFrequency(RecurrenceFrequencyType recurrenceFrequency)
        {
            RecurrenceFrequency = recurrenceFrequency;
        }

        #endregion












        #region IRecurrenceEditorViewModel Members


        private DateTime? m_StartDateTime;
        public DateTime? StartDateTime
        {
            get => m_StartDateTime;
            set => this.RaiseAndSetIfChanged(ref m_StartDateTime, value);
        }

        private RecurrenceFrequencyType m_RecurrenceFrequency;
        public RecurrenceFrequencyType RecurrenceFrequency
        {
            get => m_RecurrenceFrequency;
            set => this.RaiseAndSetIfChanged(ref m_RecurrenceFrequency, value);
        }




        private int m_Interval;
        public int Interval
        {
            get => m_Interval;
            set => this.RaiseAndSetIfChanged(ref m_Interval, value);
        }







        private bool m_IsEndNever;
        public bool IsEndNever
        {
            get => m_IsEndNever;
            set
            {
                this.RaiseAndSetIfChanged(ref m_IsEndNever, value);
                if (m_IsEndNever)
                {
                    IsEndUntil = false;
                    IsEndCount = false;
                    Until = null;
                    Count = null;
                }
            }
        }

        private bool m_IsEndUntil;
        public bool IsEndUntil
        {
            get => m_IsEndUntil;
            set
            {
                this.RaiseAndSetIfChanged(ref m_IsEndUntil, value);
                if (m_IsEndUntil)
                {
                    IsEndNever = false;
                    IsEndCount = false;
                    Count = null;
                }
            }
        }

        private bool m_IsEndCount;
        public bool IsEndCount
        {
            get => m_IsEndCount;
            set
            {
                this.RaiseAndSetIfChanged(ref m_IsEndCount, value);
                if (m_IsEndCount)
                {
                    IsEndNever = false;
                    IsEndUntil = false;
                    Until = null;
                }
            }
        }

        private DateTime? m_Until;
        public DateTime? Until
        {
            get => m_Until;
            set => this.RaiseAndSetIfChanged(ref m_Until, value);
        }

        private int? m_Count;
        public int? Count
        {
            get => m_Count;
            set => this.RaiseAndSetIfChanged(ref m_Count, value);
        }


        //// WKST (week start) as two-letter code, e.g. "MO"
        //public string WeekStart { get; set; } = "MO";


        // Weekly: 7-day flags for BYDAY
        public IList<bool> ByWeekDays { get; }











        private bool m_IsMonthDay;
        public bool IsMonthDay
        {
            get => m_IsMonthDay;
            set => this.RaiseAndSetIfChanged(ref m_IsMonthDay, value);
        }

        private bool m_IsMonthWeekday;
        public bool IsMonthWeekday
        {
            get => m_IsMonthWeekday;
            set => this.RaiseAndSetIfChanged(ref m_IsMonthWeekday, value);
        }






        // Monthly BYMONTHDAY support: 1..31, -31..-1 as needed

        private int? m_ByMonthDay;
        public int? ByMonthDay
        {
            get => m_ByMonthDay;
            set => this.RaiseAndSetIfChanged(ref m_ByMonthDay, value);
        }






        // Monthly/Yearly pattern via BYSETPOS + BYDAY

        private int? m_BySetPosSelection;
        public int? BySetPosSelection
        {
            get => m_BySetPosSelection;
            set => this.RaiseAndSetIfChanged(ref m_BySetPosSelection, value);
        }

        private string? m_ByMonthWeekdaySelection;
        public string? ByMonthWeekdaySelection
        {
            get => m_ByMonthWeekdaySelection;
            set => this.RaiseAndSetIfChanged(ref m_ByMonthWeekdaySelection, value);
        }














        // 0-11
        private int m_YearlyMonthIndex;
        public int YearlyMonthIndex
        {
            get => m_YearlyMonthIndex;
            set => this.RaiseAndSetIfChanged(ref m_YearlyMonthIndex, value);
        }

        private int m_YearlyDayOfMonth;
        public int YearlyDayOfMonth
        {
            get => m_YearlyDayOfMonth;
            set => this.RaiseAndSetIfChanged(ref m_YearlyDayOfMonth, value);
        }

        private int? m_YearlySetPosSelection;
        public int? YearlySetPosSelection
        {
            get => m_YearlySetPosSelection;
            set => this.RaiseAndSetIfChanged(ref m_YearlySetPosSelection, value);
        }

        private string? m_YearlyWeekdaySelection;
        public string? YearlyWeekdaySelection
        {
            get => m_YearlyWeekdaySelection;
            set => this.RaiseAndSetIfChanged(ref m_YearlyWeekdaySelection, value);
        }

        private int m_YearlyPatternMonthIndex;
        public int YearlyPatternMonthIndex
        {
            get => m_YearlyPatternMonthIndex;
            set => this.RaiseAndSetIfChanged(ref m_YearlyPatternMonthIndex, value);
        }












        // BYMONTH (used for yearly)
        //[Reactive]
        public IList<int> ByMonths { get; set; } = new List<int>();

        // Additional BY* fields for completeness (user can extend UI to expose them)
        //[Reactive]
        public IList<int> ByHours { get; set; } = new List<int>();
        //[Reactive]
        public IList<int> ByMinutes { get; set; } = new List<int>();
        //[Reactive]
        public IList<int> BySeconds { get; set; } = new List<int>();
        //[Reactive]
        public IList<int> ByYearDays { get; set; } = new List<int>();
        //[Reactive]
        public IList<int> ByWeekNumbers { get; set; } = new List<int>();
        //[Reactive]
        public IList<int> BySetPositions { get; set; } = new List<int>();

        //    // RSCALE/SKIP extension hooks (RFC 7529)
        //    [Reactive] public string? RScale { get; set; }
        //    [Reactive] public string? Skip { get; set; }


















        // RRULE string preview
        private string m_RRuleString;
        public string RRuleString
        {
            get => m_RRuleString;
            private set => this.RaiseAndSetIfChanged(ref m_RRuleString, value);
        }


        // Underlying iCal.NET object
        private RecurrencePattern? m_RecurrencePattern;
        public RecurrencePattern? RecurrencePattern
        {
            get => m_RecurrencePattern;
            private set => this.RaiseAndSetIfChanged(ref m_RecurrencePattern, value);
        }








        public ICommand ChangeRecurrenceFrequencyCommand { get; }













        #endregion







    }
}





//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Reactive.Linq;
//using Ical.Net;
//using Ical.Net.DataTypes;
//using ReactiveUI;
//using ReactiveUI.Fody.Helpers;

//namespace MyApp.ViewModels;


//public class RecurrenceEditorViewModel : ReactiveObject
//{
//    // Core RRULE fields
//    [Reactive] public FrequencyTypeEnum Frequency { get; set; } = FrequencyTypeEnum.None;
//    [Reactive] public int Interval { get; set; } = 1;

//    [Reactive] public bool IsEndNever { get; set; } = true;
//    [Reactive] public bool IsEndUntil { get; set; }
//    [Reactive] public bool IsEndCount { get; set; }
//    [Reactive] public DateTimeOffset? Until { get; set; }
//    [Reactive] public int? Count { get; set; }

//    // WKST (week start) as two-letter code, e.g. "MO"
//    [Reactive] public string WeekStart { get; set; } = "MO";

//    // Weekly: 7-day flags for BYDAY
//    public IList<bool> ByWeekDays { get; }

//    // Monthly BYMONTHDAY support: 1..31, -31..-1 as needed
//    public IList<int> ByMonthDaysDisplay { get; }
//    public IDictionary<int, bool> ByMonthDaysSelection { get; }

//    // Monthly/Yearly pattern via BYSETPOS + BYDAY
//    [Reactive] public int? BySetPosSelection { get; set; }
//    [Reactive] public string? ByMonthWeekdaySelection { get; set; }

//    [Reactive] public int YearlyMonthIndex { get; set; } // 0-11
//    [Reactive] public int YearlyDayOfMonth { get; set; } = 1;

//    [Reactive] public int? YearlySetPosSelection { get; set; }
//    [Reactive] public string? YearlyWeekdaySelection { get; set; }
//    [Reactive] public int YearlyPatternMonthIndex { get; set; }

//    // BYMONTH (used for yearly)
//    [Reactive] public IList<int> ByMonths { get; set; } = new List<int>();

//    // Additional BY* fields for completeness (user can extend UI to expose them)
//    [Reactive] public IList<int> ByHours { get; set; } = new List<int>();
//    [Reactive] public IList<int> ByMinutes { get; set; } = new List<int>();
//    [Reactive] public IList<int> BySeconds { get; set; } = new List<int>();
//    [Reactive] public IList<int> ByYearDays { get; set; } = new List<int>();
//    [Reactive] public IList<int> ByWeekNumbers { get; set; } = new List<int>();
//    [Reactive] public IList<int> BySetPositions { get; set; } = new List<int>();

//    // RSCALE/SKIP extension hooks (RFC 7529)
//    [Reactive] public string? RScale { get; set; }
//    [Reactive] public string? Skip { get; set; }

//    // Calculated template key for XAML dynamic region
//    [Reactive] public string DetailsTemplateKey { get; private set; } = "Simple";

//    // RRULE string preview
//    [Reactive] public string RRuleString { get; private set; } = string.Empty;

//    // Underlying iCal.NET object
//    [Reactive] public RecurrencePattern? RecurrencePattern { get; private set; }

//    public RecurrenceEditorViewModel()
//    {
//        // Weekly flags: Monday..Sunday
//        ByWeekDays = new List<bool>(Enumerable.Repeat(false, 7));

//        // Month-day display 1..31
//        ByMonthDaysDisplay = Enumerable.Range(1, 31).ToList();
//        ByMonthDaysSelection = ByMonthDaysDisplay.ToDictionary(d => d, _ => false);

//        // // Keep end flags mutually exclusive
//        this.WhenAnyValue(x => x.IsEndNever, x => x.IsEndUntil, x => x.IsEndCount)
//            .Subscribe(tuple =>
//            {
//                var (never, until, count) = tuple;
//                var trueCount = new[] { never, until, count }.Count(x => x);
//                if (trueCount == 0)
//                {
//                    IsEndNever = true;
//                }
//                else if (trueCount > 1)
//                {
//                    // Simple normalization: favor the last changed flag, but here
//                    // we reset others when one is explicitly set in the UI
//                    if (never)
//                    {
//                        IsEndUntil = false;
//                        IsEndCount = false;
//                        Until = null;
//                        Count = null;
//                    }
//                    else if (until)
//                    {
//                        IsEndNever = false;
//                        IsEndCount = false;
//                        Count = null;
//                    }
//                    else if (count)
//                    {
//                        IsEndNever = false;
//                        IsEndUntil = false;
//                        Until = null;
//                    }
//                }
//            });

//        // Compute DetailsTemplateKey from Frequency
//        this.WhenAnyValue(x => x.Frequency)
//            .Subscribe(freq =>
//            {
//                switch (freq)
//                {
//                    case FrequencyTypeEnum.Weekly:
//                        DetailsTemplateKey = "Weekly";
//                        break;
//                    case FrequencyTypeEnum.Monthly:
//                        DetailsTemplateKey = "Monthly";
//                        break;
//                    case FrequencyTypeEnum.Yearly:
//                        DetailsTemplateKey = "Yearly";
//                        break;
//                    default:
//                        DetailsTemplateKey = "Simple";
//                        break;
//                }
//            });

//        // Rebuild RecurrencePattern whenever anything changes
//        this.WhenAnyPropertyChanged()
//            .Throttle(TimeSpan.FromMilliseconds(50))
//            .ObserveOn(RxApp.MainThreadScheduler)
//            .Subscribe(_ => RebuildRecurrencePattern());
//    }

//    IObservable<object?> WhenAnyPropertyChanged()
//    {
//        return this.WhenAnyValue(
//            x => x.Frequency,
//            x => x.Interval,
//            x => x.IsEndNever,
//            x => x.IsEndUntil,
//            x => x.IsEndCount,
//            x => x.Until,
//            x => x.Count,
//            x => x.WeekStart,
//            x => x.ByWeekDays,
//            x => x.ByMonthDaysSelection,
//            x => x.BySetPosSelection,
//            x => x.ByMonthWeekdaySelection,
//            x => x.YearlyMonthIndex,
//            x => x.YearlyDayOfMonth,
//            x => x.YearlySetPosSelection,
//            x => x.YearlyWeekdaySelection,
//            x => x.YearlyPatternMonthIndex,
//            x => x.ByMonths,
//            x => x.ByHours,
//            x => x.ByMinutes,
//            x => x.BySeconds,
//            x => x.ByYearDays,
//            x => x.ByWeekNumbers,
//            x => x.BySetPositions,
//            x => x.RScale,
//            x => x.Skip,
//            (a1, a2, a3, a4, a5, a6, a7, a8,
//             a9, a10, a11, a12, a13, a14, a15,
//             a16, a17, a18, a19, a20, a21, a22,
//             a23, a24, a25, a26, a27) => (object?)null);
//    }

//    void RebuildRecurrencePattern()
//    {
//        if (Frequency == FrequencyTypeEnum.None)
//        {
//            RecurrencePattern = null;
//            RRuleString = string.Empty;
//            return;
//        }

//        var freq = FrequencyTypeToIcal(Frequency);

//        var pattern = new RecurrencePattern(freq)
//        {
//            Interval = Interval <= 0 ? 1 : Interval
//        };

//        // End conditions
//        if (IsEndUntil && Until.HasValue)
//        {
//            pattern.Until = Until.Value.UtcDateTime;
//            pattern.Count = 0;
//        }
//        else if (IsEndCount && Count.HasValue)
//        {
//            pattern.Count = Count.Value;
//            pattern.Until = null;
//        }
//        else
//        {
//            pattern.Count = 0;
//            pattern.Until = null;
//        }

//        // WKST
//        if (!string.IsNullOrWhiteSpace(WeekStart))
//        {
//            pattern.WeekStart = ParseWeekDay(WeekStart);
//        }

//        // BYDAY for weekly
//        if (Frequency == FrequencyType.Weekly)
//        {
//            pattern.ByDay.Clear();
//            var codes = new[] { "MO", "TU", "WE", "TH", "FR", "SA", "SU" };
//            for (int i = 0; i < codes.Length; i++)
//            {
//                if (ByWeekDays[i])
//                {
//                    pattern.ByDay.Add(new WeekDay(ParseWeekDay(codes[i])));
//                }
//            }
//        }

//        // MONTHLY
//        if (Frequency == FrequencyType.Monthly)
//        {
//            pattern.ByMonthDay.Clear();
//            pattern.ByDay.Clear();
//            pattern.BySetPosition.Clear();

//            // ByMonthDay from selection
//            var selectedMonthDays = ByMonthDaysSelection
//                .Where(kv => kv.Value)
//                .Select(kv => kv.Key)
//                .ToList();
//            foreach (var d in selectedMonthDays)
//            {
//                pattern.ByMonthDay.Add(d);
//            }

//            // Pattern via BySetPosSelection + ByMonthWeekdaySelection
//            if (BySetPosSelection.HasValue && !string.IsNullOrEmpty(ByMonthWeekdaySelection))
//            {
//                pattern.BySetPosition.Add(BySetPosSelection.Value);

//                var tokens = ByMonthWeekdaySelection.Split(',', StringSplitOptions.RemoveEmptyEntries);
//                foreach (var t in tokens)
//                {
//                    var wd = ParseWeekDay(t.Trim());
//                    pattern.ByDay.Add(new WeekDay(wd));
//                }
//            }
//        }

//        // YEARLY
//        if (Frequency == FrequencyType.Yearly)
//        {
//            pattern.ByMonth.Clear();
//            pattern.ByMonthDay.Clear();
//            pattern.ByDay.Clear();
//            pattern.BySetPosition.Clear();

//            // Specific date
//            if (YearlyMonthIndex >= 0 && YearlyMonthIndex <= 11 && YearlyDayOfMonth > 0)
//            {
//                pattern.ByMonth.Add(YearlyMonthIndex + 1);
//                pattern.ByMonthDay.Add(YearlyDayOfMonth);
//            }

//            // Pattern tab
//            if (YearlyPatternMonthIndex >= 0 && YearlyPatternMonthIndex <= 11
//                && YearlySetPosSelection.HasValue
//                && !string.IsNullOrEmpty(YearlyWeekdaySelection))
//            {
//                pattern.ByMonth.Clear();
//                pattern.ByMonth.Add(YearlyPatternMonthIndex + 1);

//                pattern.BySetPosition.Add(YearlySetPosSelection.Value);

//                var tokens = YearlyWeekdaySelection.Split(',', StringSplitOptions.RemoveEmptyEntries);
//                foreach (var t in tokens)
//                {
//                    var wd = ParseWeekDay(t.Trim());
//                    pattern.ByDay.Add(new WeekDay(wd));
//                }
//            }
//        }

//        // Other BY* fields
//        CopyList(pattern.ByHour, ByHours);
//        CopyList(pattern.ByMinute, ByMinutes);
//        CopyList(pattern.BySecond, BySeconds);
//        CopyList(pattern.ByYearDay, ByYearDays);
//        CopyList(pattern.ByWeekNo, ByWeekNumbers);
//        CopyList(pattern.BySetPosition, BySetPositions);

//        // BYMONTH (explicit)
//        pattern.ByMonth.Clear();
//        foreach (var m in ByMonths.Distinct())
//        {
//            pattern.ByMonth.Add(m);
//        }

//        // RSCALE/SKIP extensions are not directly supported by iCal.NET RecurrencePattern,
//        // but you can inject them into the ToString output if needed.
//        RecurrencePattern = pattern;
//        RRuleString = pattern.ToString();

//        if (!string.IsNullOrEmpty(RScale))
//        {
//            RRuleString += $";RSCALE={RScale}";
//        }
//        if (!string.IsNullOrEmpty(Skip))
//        {
//            RRuleString += $";SKIP={Skip}";
//        }
//    }

//    static void CopyList(IList<int> target, IList<int> source)
//    {
//        target.Clear();
//        foreach (var v in source.Distinct())
//            target.Add(v);
//    }

//    static FrequencyTypeEnum FrequencyTypeToIcal(FrequencyType freq)
//    {
//        return freq switch
//        {
//            FrequencyType.Secondly => FrequencyTypeEnum.Secondly,
//            FrequencyType.Minutely => FrequencyTypeEnum.Minutely,
//            FrequencyType.Hourly => FrequencyTypeEnum.Hourly,
//            FrequencyType.Daily => FrequencyTypeEnum.Daily,
//            FrequencyType.Weekly => FrequencyTypeEnum.Weekly,
//            FrequencyType.Monthly => FrequencyTypeEnum.Monthly,
//            FrequencyType.Yearly => FrequencyTypeEnum.Yearly,
//            _ => FrequencyTypeEnum.None
//        };
//    }

//    static DayOfWeek ParseWeekDay(string code)
//    {
//        return code.ToUpperInvariant() switch
//        {
//            "MO" => DayOfWeek.Monday,
//            "TU" => DayOfWeek.Tuesday,
//            "WE" => DayOfWeek.Wednesday,
//            "TH" => DayOfWeek.Thursday,
//            "FR" => DayOfWeek.Friday,
//            "SA" => DayOfWeek.Saturday,
//            "SU" => DayOfWeek.Sunday,
//            _ => DayOfWeek.Monday
//        };
//    }

//    // Populate from an existing RecurrencePattern (e.g., when editing)
//    public void LoadFromPattern(RecurrencePattern? pattern)
//    {
//        if (pattern == null)
//        {
//            Frequency = FrequencyType.None;
//            return;
//        }

//        Frequency = pattern.Frequency switch
//        {
//            FrequencyType.Secondly => FrequencyTypeEnum.Secondly,
//            FrequencyType.Minutely => FrequencyTypeEnum.Minutely,
//            FrequencyType.Hourly => FrequencyTypeEnum.Hourly,
//            FrequencyType.Daily => FrequencyTypeEnum.Daily,
//            FrequencyType.Weekly => FrequencyTypeEnum.Weekly,
//            FrequencyType.Monthly => FrequencyTypeEnum.Monthly,
//            FrequencyType.Yearly => FrequencyTypeEnum.Yearly,
//            _ => FrequencyTypeEnum.None
//        };

//        Interval = pattern.Interval <= 0 ? 1 : pattern.Interval;

//        if (pattern.Count > 0)
//        {
//            IsEndCount = true;
//            IsEndNever = false;
//            IsEndUntil = false;
//            Count = pattern.Count;
//            Until = null;
//        }
//        else if (pattern.Until != DateTime.MinValue)
//        {
//            IsEndUntil = true;
//            IsEndNever = false;
//            IsEndCount = false;
//            Until = new DateTimeOffset(pattern.Until, TimeSpan.Zero);
//            Count = null;
//        }
//        else
//        {
//            IsEndNever = true;
//            IsEndUntil = false;
//            IsEndCount = false;
//            Count = null;
//            Until = null;
//        }

//        WeekStart = pattern.WeekStart switch
//        {
//            DayOfWeek.Monday => "MO",
//            DayOfWeek.Tuesday => "TU",
//            DayOfWeek.Wednesday => "WE",
//            DayOfWeek.Thursday => "TH",
//            DayOfWeek.Friday => "FR",
//            DayOfWeek.Saturday => "SA",
//            DayOfWeek.Sunday => "SU",
//            _ => "MO"
//        };

//        // Weekly
//        if (Frequency == FrequencyType.Weekly)
//        {
//            var codes = new[] { "MO", "TU", "WE", "TH", "FR", "SA", "SU" };
//            for (int i = 0; i < ByWeekDays.Count; i++)
//            {
//                var wd = ParseWeekDay(codes[i]);
//                ByWeekDays[i] = pattern.ByDay.Any(b => b.DayOfWeek == wd);
//            }
//        }

//        // Monthly
//        if (Frequency == FrequencyType.Monthly)
//        {
//            foreach (var key in ByMonthDaysDisplay.ToList())
//            {
//                ByMonthDaysSelection[key] = pattern.ByMonthDay.Contains(key);
//            }

//            if (pattern.BySetPosition.Any() && pattern.ByDay.Any())
//            {
//                BySetPosSelection = pattern.BySetPosition.First();
//                var wds = pattern.ByDay.Select(b => WeekDayToCode(b.DayOfWeek)).ToArray();
//                ByMonthWeekdaySelection = string.Join(",", wds);
//            }
//            else
//            {
//                BySetPosSelection = null;
//                ByMonthWeekdaySelection = null;
//            }
//        }

//        // Yearly
//        if (Frequency == FrequencyType.Yearly)
//        {
//            if (pattern.ByMonth.Any() && pattern.ByMonthDay.Any())
//            {
//                YearlyMonthIndex = pattern.ByMonth.First() - 1;
//                YearlyDayOfMonth = pattern.ByMonthDay.First();
//            }

//            if (pattern.ByMonth.Any() && pattern.BySetPosition.Any() && pattern.ByDay.Any())
//            {
//                YearlyPatternMonthIndex = pattern.ByMonth.First() - 1;
//                YearlySetPosSelection = pattern.BySetPosition.First();
//                YearlyWeekdaySelection = string.Join(",", pattern.ByDay.Select(b => WeekDayToCode(b.DayOfWeek)));
//            }
//        }

//        ByMonths = pattern.ByMonth.ToList();
//        ByHours = pattern.ByHour.ToList();
//        ByMinutes = pattern.ByMinute.ToList();
//        BySeconds = pattern.BySecond.ToList();
//        ByYearDays = pattern.ByYearDay.ToList();
//        ByWeekNumbers = pattern.ByWeekNo.ToList();
//        BySetPositions = pattern.BySetPosition.ToList();

//        RecurrencePattern = pattern;
//        RRuleString = pattern.ToString();
//    }

//    static string WeekDayToCode(DayOfWeek day)
//    {
//        return day switch
//        {
//            DayOfWeek.Monday => "MO",
//            DayOfWeek.Tuesday => "TU",
//            DayOfWeek.Wednesday => "WE",
//            DayOfWeek.Thursday => "TH",
//            DayOfWeek.Friday => "FR",
//            DayOfWeek.Saturday => "SA",
//            DayOfWeek.Sunday => "SU",
//            _ => "MO"
//        };
//    }
//}
