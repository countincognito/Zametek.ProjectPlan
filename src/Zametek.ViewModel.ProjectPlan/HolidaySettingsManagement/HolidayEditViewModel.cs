using Ical.Net;
using Ical.Net.DataTypes;
using ReactiveUI;
using System.Reactive.Linq;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class HolidayEditViewModel
        : ViewModelBase, IHolidayEditViewModel
    {
        #region Fields

        private readonly IDisposable? m_ReviseRecurrencePattern01Sub;
        private readonly IDisposable? m_ReviseRecurrencePattern02Sub;
        private readonly IDisposable? m_ReviseRecurrencePattern03Sub;
        private readonly IDisposable? m_ReviseRecurrencePattern04Sub;

        #endregion

        #region Ctors

        public HolidayEditViewModel()
        {
            m_StartDateTime = null;
            m_RecurrenceFrequency = RecurrenceFrequencyType.Daily;
            m_Interval = 1;
            m_IsEndNever = true;
            m_IsEndUntil = false;
            m_IsEndCount = false;
            m_Until = null;
            m_Count = null;

            // Weekly flags: Monday..Sunday
            ByWeekDaysMonday = false;
            ByWeekDaysTuesday = false;
            ByWeekDaysWednesday = false;
            ByWeekDaysThursday = false;
            ByWeekDaysFriday = false;
            ByWeekDaysSaturday = false;
            ByWeekDaysSunday = false;

            // Month-day display 1..31
            m_IsMonthDay = true;
            m_ByMonthDay = 1;

            m_IsMonthWeekday = false;

            ByMonthSetPosOptions = [
                new SetByIntModel { Name = "First", Content = 1 },
                new SetByIntModel { Name = "Second", Content = 2 },
                new SetByIntModel { Name = "Third", Content = 3 },
                new SetByIntModel { Name = "Fourth", Content = 4 },
                new SetByIntModel { Name = "Last", Content = -1 }
            ];

            ByMonthWeekdayOptions = [
                new SetByStringModel { Name = "Monday", Content = "MO" },
                new SetByStringModel { Name = "Tuesday", Content = "TU" },
                new SetByStringModel { Name = "Wednesday", Content = "WE" },
                new SetByStringModel { Name = "Thursday", Content = "TH" },
                new SetByStringModel { Name = "Friday", Content = "FR" },
                new SetByStringModel { Name = "Saturday", Content = "SA" },
                new SetByStringModel { Name = "Sunday", Content = "SU" },
                new SetByStringModel { Name = "Week day", Content = "MO,TU,WE,TH,FR" },
                new SetByStringModel { Name = "Weekend day", Content = "SA,SU" }
            ];

            // Yearly

            m_IsYearlyMonthDay = true;

            m_IsYearlyMonthWeekday = false;

            YearlySetPosOptions = [
                new SetByIntModel { Name = "First", Content = 1 },
                new SetByIntModel { Name = "Second", Content = 2 },
                new SetByIntModel { Name = "Third", Content = 3 },
                new SetByIntModel { Name = "Fourth", Content = 4 },
                new SetByIntModel { Name = "Last", Content = -1 }
            ];

            YearlyWeekdayOptions = [
                new SetByStringModel { Name = "Monday", Content = "MO" },
                new SetByStringModel { Name = "Tuesday", Content = "TU" },
                new SetByStringModel { Name = "Wednesday", Content = "WE" },
                new SetByStringModel { Name = "Thursday", Content = "TH" },
                new SetByStringModel { Name = "Friday", Content = "FR" },
                new SetByStringModel { Name = "Saturday", Content = "SA" },
                new SetByStringModel { Name = "Sunday", Content = "SU" },
                new SetByStringModel { Name = "Week day", Content = "MO,TU,WE,TH,FR" },
                new SetByStringModel { Name = "Weekend day", Content = "SA,SU" }
            ];

            YearlyMonthOptions = [
                new SetByIntModel { Name = "January", Content = 1 },
                new SetByIntModel { Name = "February", Content = 2 },
                new SetByIntModel { Name = "March", Content = 3 },
                new SetByIntModel { Name = "April", Content = 4 },
                new SetByIntModel { Name = "May", Content = 5 },
                new SetByIntModel { Name = "June", Content = 6 },
                new SetByIntModel { Name = "July", Content = 7 },
                new SetByIntModel { Name = "August", Content = 8 },
                new SetByIntModel { Name = "September", Content = 9 },
                new SetByIntModel { Name = "October", Content = 10 },
                new SetByIntModel { Name = "November", Content = 11 },
                new SetByIntModel { Name = "December", Content = 12 }
            ];





            m_RecurrencePattern = string.Empty;

            ChangeRecurrenceFrequencyCommand = ReactiveCommand.Create<RecurrenceFrequencyType>(ChangeRecurrenceFrequency);

            m_ReviseRecurrencePattern01Sub = this
                .WhenAnyValue(
                    x => x.StartDateTime,
                    x => x.RecurrenceFrequency,
                    x => x.Interval,
                    x => x.IsEndNever,
                    x => x.IsEndUntil,
                    x => x.IsEndCount,
                    x => x.Until)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => RebuildRecurrencePattern());

            m_ReviseRecurrencePattern02Sub = this
                .WhenAnyValue(
                    x => x.ByWeekDaysMonday,
                    x => x.ByWeekDaysTuesday,
                    x => x.ByWeekDaysWednesday,
                    x => x.ByWeekDaysThursday,
                    x => x.ByWeekDaysFriday,
                    x => x.ByWeekDaysSaturday,
                    x => x.ByWeekDaysSunday)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => RebuildRecurrencePattern());

            m_ReviseRecurrencePattern03Sub = this
                .WhenAnyValue(
                    x => x.Count,
                    x => x.IsMonthDay,
                    x => x.IsMonthWeekday,
                    x => x.ByMonthDay,
                    x => x.ByMonthSetPosSelection,
                    x => x.ByMonthWeekdaySelection)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => RebuildRecurrencePattern());

            m_ReviseRecurrencePattern04Sub = this
                .WhenAnyValue(
                    x => x.IsYearlyMonthDay,
                    x => x.IsYearlyMonthWeekday,
                    x => x.YearlyDayOfMonth,
                    x => x.YearlySetPosSelection,
                    x => x.YearlyWeekdaySelection,
                    x => x.YearlyMonthSelection)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => RebuildRecurrencePattern());
        }

        #endregion

        #region Properties

        //// WKST (week start) as two-letter code, e.g. "MO"
        //public string WeekStart { get; set; } = "MO";

        //    // RSCALE/SKIP extension hooks (RFC 7529)
        //    [Reactive] public string? RScale { get; set; }
        //    [Reactive] public string? Skip { get; set; }

        // BYMONTH (used for yearly)
        public List<int> ByMonths { get; set; } = [];

        // Additional BY* fields for completeness (user can extend UI to expose them)
        public List<int> ByHours { get; set; } = [];

        public List<int> ByMinutes { get; set; } = [];

        public List<int> BySeconds { get; set; } = [];

        public List<int> ByYearDays { get; set; } = [];

        public List<int> ByWeekNumbers { get; set; } = [];

        public List<int> BySetPositions { get; set; } = [];

        #endregion




        #region Private Members

        void RebuildRecurrencePattern()
        {
            //if (RecurrenceFrequency == RecurrenceFrequencyType.Daily)
            //{
            //    RecurrencePattern = string.Empty;
            //    return;
            //}

            FrequencyType frequencyType = FrequencyTypeToIcal(RecurrenceFrequency);

            var pattern = new RecurrencePattern(frequencyType)
            {
                Interval = Interval <= 0 ? 1 : Interval
            };


            // Start





            // End conditions
            if (IsEndUntil && Until.HasValue)
            {
                pattern.Until = new CalDateTime(Until.Value, false);
                pattern.Count = null;
            }
            else if (IsEndCount && Count.HasValue)
            {
                pattern.Count = Count.Value;
                pattern.Until = null;
            }
            else
            {
                pattern.Count = null;
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
                if (ByWeekDaysMonday)
                {
                    pattern.ByDay.Add(new WeekDay(System.DayOfWeek.Monday));
                }
                if (ByWeekDaysTuesday)
                {
                    pattern.ByDay.Add(new WeekDay(DayOfWeek.Tuesday));
                }
                if (ByWeekDaysWednesday)
                {
                    pattern.ByDay.Add(new WeekDay(DayOfWeek.Wednesday));
                }
                if (ByWeekDaysThursday)
                {
                    pattern.ByDay.Add(new WeekDay(DayOfWeek.Thursday));
                }
                if (ByWeekDaysFriday)
                {
                    pattern.ByDay.Add(new WeekDay(DayOfWeek.Friday));
                }
                if (ByWeekDaysSaturday)
                {
                    pattern.ByDay.Add(new WeekDay(DayOfWeek.Saturday));
                }
                if (ByWeekDaysSunday)
                {
                    pattern.ByDay.Add(new WeekDay(DayOfWeek.Sunday));
                }
            }

















            // MONTHLY
            if (RecurrenceFrequency == RecurrenceFrequencyType.Monthly)
            {
                pattern.ByMonthDay.Clear();
                pattern.ByDay.Clear();
                pattern.BySetPosition.Clear();

                // ByMonthDay
                if (ByMonthDay.HasValue
                    && ByMonthDay.GetValueOrDefault() > 0)
                {
                    pattern.ByMonthDay.Add(ByMonthDay.GetValueOrDefault());
                }

                // Pattern via BySetPosSelection + ByMonthWeekdaySelection
                if (ByMonthSetPosSelection is not null
                    && ByMonthWeekdaySelection is not null)
                {
                    pattern.BySetPosition.Add(ByMonthSetPosSelection.Content);

                    var tokens = ByMonthWeekdaySelection.Content.Split(',', StringSplitOptions.RemoveEmptyEntries);
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
                if (YearlyMonthSelection is not null
                    && YearlyMonthSelection.Content >= 1 && YearlyMonthSelection.Content <= 12
                    && YearlyDayOfMonth.HasValue
                    && YearlyDayOfMonth.GetValueOrDefault() > 0)
                {
                    pattern.ByMonth.Add(YearlyMonthSelection.Content);
                    pattern.ByMonthDay.Add(YearlyDayOfMonth.GetValueOrDefault());
                }

                // Pattern via YearlySetPosSelection + YearlyWeekdaySelection + YearlyMonthSelection
                if (YearlyMonthSelection is not null
                    && YearlyMonthSelection.Content >= 1 && YearlyMonthSelection.Content <= 12
                    && YearlySetPosSelection is not null
                    && YearlyWeekdaySelection is not null)
                {
                    pattern.ByMonth.Clear();
                    pattern.ByMonth.Add(YearlyMonthSelection.Content);

                    pattern.BySetPosition.Add(YearlySetPosSelection.Content);

                    var tokens = YearlyWeekdaySelection.Content.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var t in tokens)
                    {
                        var wd = ParseWeekDay(t.Trim());
                        pattern.ByDay.Add(new WeekDay(wd));
                    }
                }
            }

            // Other BY* fields
            //CopyList(pattern.ByHour, ByHours);
            //CopyList(pattern.ByMinute, ByMinutes);
            //CopyList(pattern.BySecond, BySeconds);
            //CopyList(pattern.ByYearDay, ByYearDays);
            //CopyList(pattern.ByWeekNo, ByWeekNumbers);
            //CopyList(pattern.BySetPosition, BySetPositions);

            // BYMONTH (explicit)
            //pattern.ByMonth.Clear();
            //foreach (var m in ByMonths.Distinct())
            //{
            //    pattern.ByMonth.Add(m);
            //}

            // RSCALE/SKIP extensions are not directly supported by iCal.NET RecurrencePattern,
            // but you can inject them into the ToString output if needed.
            RecurrencePattern = pattern?.ToString() ?? string.Empty;

            //if (!string.IsNullOrEmpty(RScale))
            //{
            //    RecurrenceRule += $";RSCALE={RScale}";
            //}
            //if (!string.IsNullOrEmpty(Skip))
            //{
            //    RecurrenceRule += $";SKIP={Skip}";
            //}
        }

        private static void CopyList(
            List<int> target,
            List<int> source)
        {
            target.Clear();
            foreach (int val in source.Distinct())
            {
                target.Add(val);
            }
        }

        private static FrequencyType FrequencyTypeToIcal(RecurrenceFrequencyType frequencyType)
        {
            return frequencyType switch
            {
                //RecurrenceFrequencyType.Secondly => FrequencyType.Secondly,
                //RecurrenceFrequencyType.Minutely => FrequencyType.Minutely,
                //RecurrenceFrequencyType.Hourly => FrequencyType.Hourly,
                RecurrenceFrequencyType.Daily => FrequencyType.Daily,
                RecurrenceFrequencyType.Weekly => FrequencyType.Weekly,
                RecurrenceFrequencyType.Monthly => FrequencyType.Monthly,
                RecurrenceFrequencyType.Yearly => FrequencyType.Yearly,
                _ => throw new ArgumentOutOfRangeException(nameof(frequencyType)),
            };
        }

        private static DayOfWeek ParseWeekDay(string code)
        {
            return code.Trim().ToUpperInvariant() switch
            {
                @"MO" => DayOfWeek.Monday,
                @"TU" => DayOfWeek.Tuesday,
                @"WE" => DayOfWeek.Wednesday,
                @"TH" => DayOfWeek.Thursday,
                @"FR" => DayOfWeek.Friday,
                @"SA" => DayOfWeek.Saturday,
                @"SU" => DayOfWeek.Sunday,
                _ => DayOfWeek.Monday
            };
        }



        private SetByIntModel? ByMonthSetPositionToModel(int position)
        {
            return ByMonthSetPosOptions.FirstOrDefault(s => s.Content == position);
        }



        private SetByStringModel? ByMonthWeekdayToModel(string? monthWeekday)
        {
            return ByMonthWeekdayOptions.FirstOrDefault(s => s.Content.Equals(monthWeekday, StringComparison.OrdinalIgnoreCase));
        }


        private static string WeekDayToCode(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Monday => @"MO",
                DayOfWeek.Tuesday => @"TU",
                DayOfWeek.Wednesday => @"WE",
                DayOfWeek.Thursday => @"TH",
                DayOfWeek.Friday => @"FR",
                DayOfWeek.Saturday => @"SA",
                DayOfWeek.Sunday => @"SU",
                _ => "MO"
            };
        }


        private SetByIntModel? YearlySetPositionToModel(int position)
        {
            return YearlySetPosOptions.FirstOrDefault(m => m.Content == position);
        }


        private SetByIntModel? YearlyMonthToModel(int month)
        {
            return YearlyMonthOptions.FirstOrDefault(m => m.Content == month);
        }




        private SetByStringModel? YearlyWeekdayToModel(string? monthWeekday)
        {
            return YearlyWeekdayOptions.FirstOrDefault(m => m.Content.Equals(monthWeekday, StringComparison.OrdinalIgnoreCase));
        }











        private void ChangeRecurrenceFrequency(RecurrenceFrequencyType recurrenceFrequency)
        {
            RecurrenceFrequency = recurrenceFrequency;
        }
        #endregion

        #region IHolidayEditViewModel Members






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

        // Weekly: 7-day flags for BYDAY

        private bool m_ByWeekDaysMonday;
        public bool ByWeekDaysMonday
        {
            get => m_ByWeekDaysMonday;
            set => this.RaiseAndSetIfChanged(ref m_ByWeekDaysMonday, value);
        }

        private bool m_ByWeekDaysTuesday;
        public bool ByWeekDaysTuesday
        {
            get => m_ByWeekDaysTuesday;
            set => this.RaiseAndSetIfChanged(ref m_ByWeekDaysTuesday, value);
        }

        private bool m_ByWeekDaysWednesday;
        public bool ByWeekDaysWednesday
        {
            get => m_ByWeekDaysWednesday;
            set => this.RaiseAndSetIfChanged(ref m_ByWeekDaysWednesday, value);
        }

        private bool m_ByWeekDaysThursday;
        public bool ByWeekDaysThursday
        {
            get => m_ByWeekDaysThursday;
            set => this.RaiseAndSetIfChanged(ref m_ByWeekDaysThursday, value);
        }

        private bool m_ByWeekDaysFriday;
        public bool ByWeekDaysFriday
        {
            get => m_ByWeekDaysFriday;
            set => this.RaiseAndSetIfChanged(ref m_ByWeekDaysFriday, value);
        }

        private bool m_ByWeekDaysSaturday;
        public bool ByWeekDaysSaturday
        {
            get => m_ByWeekDaysSaturday;
            set => this.RaiseAndSetIfChanged(ref m_ByWeekDaysSaturday, value);
        }

        private bool m_ByWeekDaysSunday;
        public bool ByWeekDaysSunday
        {
            get => m_ByWeekDaysSunday;
            set => this.RaiseAndSetIfChanged(ref m_ByWeekDaysSunday, value);
        }

        // Monthly

        private bool m_IsMonthDay;
        public bool IsMonthDay
        {
            get => m_IsMonthDay;
            set
            {
                this.RaiseAndSetIfChanged(ref m_IsMonthDay, value);
                if (m_IsMonthDay)
                {
                    IsMonthWeekday = false;
                    ByMonthSetPosSelection = null;
                    ByMonthWeekdaySelection = null;
                }
            }
        }

        // Monthly BYMONTHDAY support: 1..31, -31..-1 as needed

        private int? m_ByMonthDay;
        public int? ByMonthDay
        {
            get => m_ByMonthDay;
            set => this.RaiseAndSetIfChanged(ref m_ByMonthDay, value);
        }

        private bool m_IsMonthWeekday;
        public bool IsMonthWeekday
        {
            get => m_IsMonthWeekday;
            set
            {
                this.RaiseAndSetIfChanged(ref m_IsMonthWeekday, value);
                if (m_IsMonthWeekday)
                {
                    IsMonthDay = false;
                    ByMonthDay = null;
                }
            }
        }

        // Monthly/Yearly pattern via BYSETPOS + BYDAY

        private SetByIntModel? m_ByMonthSetPosSelection;
        public SetByIntModel? ByMonthSetPosSelection
        {
            get => m_ByMonthSetPosSelection;
            set => this.RaiseAndSetIfChanged(ref m_ByMonthSetPosSelection, value);
        }
        public List<SetByIntModel> ByMonthSetPosOptions { get; }

        private SetByStringModel? m_ByMonthWeekdaySelection;
        public SetByStringModel? ByMonthWeekdaySelection
        {
            get => m_ByMonthWeekdaySelection;
            set => this.RaiseAndSetIfChanged(ref m_ByMonthWeekdaySelection, value);
        }
        public List<SetByStringModel> ByMonthWeekdayOptions { get; }

        // Yearly

        private bool m_IsYearlyMonthDay;
        public bool IsYearlyMonthDay
        {
            get => m_IsYearlyMonthDay;
            set
            {
                this.RaiseAndSetIfChanged(ref m_IsYearlyMonthDay, value);
                if (m_IsYearlyMonthDay)
                {
                    IsYearlyMonthWeekday = false;
                    YearlySetPosSelection = null;
                    YearlyWeekdaySelection = null;
                    //YearlyMonthSelection = null;
                }
            }
        }

        private int? m_YearlyDayOfMonth;
        public int? YearlyDayOfMonth
        {
            get => m_YearlyDayOfMonth;
            set => this.RaiseAndSetIfChanged(ref m_YearlyDayOfMonth, value);
        }

        private bool m_IsYearlyMonthWeekday;
        public bool IsYearlyMonthWeekday
        {
            get => m_IsYearlyMonthWeekday;
            set
            {
                this.RaiseAndSetIfChanged(ref m_IsYearlyMonthWeekday, value);
                if (m_IsYearlyMonthWeekday)
                {
                    IsYearlyMonthDay = false;
                    YearlyDayOfMonth = null;
                }
            }
        }



        private SetByIntModel? m_YearlySetPosSelection;
        public SetByIntModel? YearlySetPosSelection
        {
            get => m_YearlySetPosSelection;
            set => this.RaiseAndSetIfChanged(ref m_YearlySetPosSelection, value);
        }
        public List<SetByIntModel> YearlySetPosOptions { get; }



        private SetByStringModel? m_YearlyWeekdaySelection;
        public SetByStringModel? YearlyWeekdaySelection
        {
            get => m_YearlyWeekdaySelection;
            set => this.RaiseAndSetIfChanged(ref m_YearlyWeekdaySelection, value);
        }
        public List<SetByStringModel> YearlyWeekdayOptions { get; }



        private SetByIntModel? m_YearlyMonthSelection;
        public SetByIntModel? YearlyMonthSelection
        {
            get => m_YearlyMonthSelection;
            set => this.RaiseAndSetIfChanged(ref m_YearlyMonthSelection, value);
        }
        public List<SetByIntModel> YearlyMonthOptions { get; }



        // RRULE string preview
        //private string m_RecurrencePattern;
        //public string RecurrencePattern
        //{
        //    get => m_RecurrencePattern;
        //    private set => this.RaiseAndSetIfChanged(ref m_RecurrencePattern, value);
        //}


        private string m_RecurrencePattern;
        public string RecurrencePattern
        {
            get => m_RecurrencePattern;
            set
            {
                this.RaiseAndSetIfChanged(ref m_RecurrencePattern, value);
                this.RaisePropertyChanged(nameof(RecurrencePatternDisplay));
            }
        }

        public string RecurrencePatternDisplay
        {
            get => RecurrenceRuleHelper.ToPhrase(RecurrencePatternHelper.ToRule(RecurrencePattern));
        }





        public ICommand ChangeRecurrenceFrequencyCommand { get; }


        // Populate from an existing RecurrencePattern (e.g., when editing)
        public void LoadFromPattern(string recurrencePattern)
        {
            var pattern = new RecurrencePattern(recurrencePattern);

            //if (pattern == null)
            //{

            //    RecurrenceFrequency = RecurrenceFrequencyType.None;
            //    return;
            //}

            RecurrenceFrequency = pattern.Frequency switch
            {
                //FrequencyType.Secondly => RecurrenceFrequencyType.Secondly,
                //FrequencyType.Minutely => RecurrenceFrequencyType.Minutely,
                //FrequencyType.Hourly => RecurrenceFrequencyType.Hourly,
                FrequencyType.Daily => RecurrenceFrequencyType.Daily,
                FrequencyType.Weekly => RecurrenceFrequencyType.Weekly,
                FrequencyType.Monthly => RecurrenceFrequencyType.Monthly,
                FrequencyType.Yearly => RecurrenceFrequencyType.Yearly,
                _ => RecurrenceFrequencyType.Daily
            };

            Interval = pattern.Interval <= 0 ? 1 : pattern.Interval;

            if (pattern.Count > 0)
            {
                IsEndCount = true;
                Count = pattern.Count;
            }
            else if (pattern.Until != new CalDateTime(DateTime.MinValue))
            {
                IsEndUntil = true;
                Until = pattern.Until?.Value;
            }
            else
            {
                IsEndNever = true;
            }

            //WeekStart = pattern.WeekStart switch
            //{
            //    DayOfWeek.Monday => "MO",
            //    DayOfWeek.Tuesday => "TU",
            //    DayOfWeek.Wednesday => "WE",
            //    DayOfWeek.Thursday => "TH",
            //    DayOfWeek.Friday => "FR",
            //    DayOfWeek.Saturday => "SA",
            //    DayOfWeek.Sunday => "SU",
            //    _ => "MO"
            //};

            // Weekly
            if (RecurrenceFrequency == RecurrenceFrequencyType.Weekly)
            {
                HashSet<DayOfWeek> byDays = [.. pattern.ByDay.Select(x => x.DayOfWeek)];

                if (byDays.Contains(DayOfWeek.Monday))
                {
                    ByWeekDaysMonday = true;
                }
                if (byDays.Contains(DayOfWeek.Tuesday))
                {
                    ByWeekDaysTuesday = true;
                }
                if (byDays.Contains(DayOfWeek.Wednesday))
                {
                    ByWeekDaysWednesday = true;
                }
                if (byDays.Contains(DayOfWeek.Thursday))
                {
                    ByWeekDaysThursday = true;
                }
                if (byDays.Contains(DayOfWeek.Friday))
                {
                    ByWeekDaysFriday = true;
                }
                if (byDays.Contains(DayOfWeek.Saturday))
                {
                    ByWeekDaysSaturday = true;
                }
                if (byDays.Contains(DayOfWeek.Sunday))
                {
                    ByWeekDaysSunday = true;
                }
            }

            // Monthly
            if (RecurrenceFrequency == RecurrenceFrequencyType.Monthly)
            {
                //foreach (var key in ByMonthDaysDisplay.ToList())
                //{
                //    ByMonthDaysSelection[key] = pattern.ByMonthDay.Contains(key);
                //}

                if (pattern.ByMonthDay.Count != 0)
                {
                    ByMonthDay = pattern.ByMonthDay.First();
                    IsMonthDay = true;
                }
                else
                {
                    ByMonthDay = null;
                    IsMonthDay = false;
                }

                if (pattern.BySetPosition.Count != 0
                    && pattern.ByDay.Count != 0)
                {
                    ByMonthSetPosSelection = ByMonthSetPositionToModel(pattern.BySetPosition.FirstOrDefault());
                    var wds = pattern.ByDay.Select(b => WeekDayToCode(b.DayOfWeek)).ToArray();
                    ByMonthWeekdaySelection = ByMonthWeekdayToModel(string.Join(",", wds));
                    IsMonthWeekday = true;
                }
                else
                {
                    ByMonthSetPosSelection = null;
                    ByMonthWeekdaySelection = null;
                    IsMonthWeekday = false;
                }
            }

            // Yearly
            if (RecurrenceFrequency == RecurrenceFrequencyType.Yearly)
            {

                if (pattern.ByMonth.Count != 0
                    && pattern.ByMonthDay.Count != 0)
                {
                    YearlyMonthSelection = YearlyMonthToModel(pattern.ByMonth.FirstOrDefault());

                    YearlyDayOfMonth = pattern.ByMonthDay.First();
                    IsYearlyMonthDay = true;
                }
                else
                {
                    YearlyDayOfMonth = null;
                    IsYearlyMonthDay = false;

                }

                if (pattern.ByMonth.Count != 0
                    && pattern.BySetPosition.Count != 0
                    && pattern.ByDay.Count != 0)
                {


                    YearlyMonthSelection = YearlyMonthToModel(pattern.ByMonth.FirstOrDefault());
                    YearlySetPosSelection = YearlySetPositionToModel(pattern.BySetPosition.FirstOrDefault());
                    YearlyWeekdaySelection = YearlyWeekdayToModel(string.Join(",", pattern.ByDay.Select(b => WeekDayToCode(b.DayOfWeek))));



                    //YearlyPatternMonthIndex = pattern.ByMonth.First() - 1;
                    //YearlySetPosSelection = pattern.BySetPosition.First();
                    //YearlyWeekdaySelection = string.Join(",", pattern.ByDay.Select(b => WeekDayToCode(b.DayOfWeek)));

                    IsYearlyMonthWeekday = true;
                }
                else
                {
                    IsYearlyMonthWeekday = false;
                    YearlySetPosSelection = null;
                    YearlyWeekdaySelection = null;
                }


            }

            //ByMonths = [.. pattern.ByMonth];
            //ByHours = [.. pattern.ByHour];
            //ByMinutes = [.. pattern.ByMinute];
            //BySeconds = [.. pattern.BySecond];
            //ByYearDays = [.. pattern.ByYearDay];
            //ByWeekNumbers = [.. pattern.ByWeekNo];
            //BySetPositions = [.. pattern.BySetPosition];

            //RecurrencePattern = pattern;
            RecurrencePattern = pattern?.ToString() ?? string.Empty;
        }

















        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
            m_ReviseRecurrencePattern01Sub?.Dispose();
            m_ReviseRecurrencePattern02Sub?.Dispose();
            m_ReviseRecurrencePattern03Sub?.Dispose();
            m_ReviseRecurrencePattern04Sub?.Dispose();
        }

        #endregion

        #region IDisposable Members

        private bool m_Disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (m_Disposed)
            {
                return;
            }

            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
                KillSubscriptions();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

            m_Disposed = true;
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
