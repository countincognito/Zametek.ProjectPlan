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

        private readonly IDateTimeCalculator m_DateTimeCalculator;

        private readonly IDisposable? m_ReviseRecurrencePattern01Sub;
        private readonly IDisposable? m_ReviseRecurrencePattern02Sub;
        private readonly IDisposable? m_ReviseRecurrencePattern03Sub;
        private readonly IDisposable? m_ReviseRecurrencePattern04Sub;

        #endregion

        #region Ctors

        public HolidayEditViewModel(
            IManagedHolidayViewModel managedHolidayViewModel,
            IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(managedHolidayViewModel);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            m_DateTimeCalculator = dateTimeCalculator;
            m_StartDateTime = managedHolidayViewModel.StartDateTime;

            m_RecurrenceFrequency = RecurrenceFrequency.None;
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

            ByMonthSetPosOptions = DefaultByMonthSetPosOptions();
            ByMonthWeekdayOptions = DefaultByMonthWeekdayOptions();

            // Yearly
            m_IsYearlyMonthDay = true;

            m_IsYearlyMonthWeekday = false;

            YearlySetPosOptions = DefaultYearlySetPosOptions();
            YearlyWeekdayOptions = DefaultYearlyWeekdayOptions();
            YearlyMonthOptions = DefaultYearlyMonthOptions();

            ChangeRecurrenceFrequencyCommand = ReactiveCommand.Create<RecurrenceFrequency>(ChangeRecurrenceFrequency);

            m_ReviseRecurrencePattern01Sub = this
                .WhenAnyValue(
                    x => x.StartDateTime,
                    x => x.RecurrenceFrequency,
                    x => x.Interval,
                    x => x.IsEndNever,
                    x => x.IsEndUntil,
                    x => x.IsEndCount,
                    x => x.Until)
                .ObserveOn(RxSchedulers.TaskpoolScheduler)
                .Subscribe(_ => RebuildRecurrenceRule());

            m_ReviseRecurrencePattern02Sub = this
                .WhenAnyValue(
                    x => x.ByWeekDaysMonday,
                    x => x.ByWeekDaysTuesday,
                    x => x.ByWeekDaysWednesday,
                    x => x.ByWeekDaysThursday,
                    x => x.ByWeekDaysFriday,
                    x => x.ByWeekDaysSaturday,
                    x => x.ByWeekDaysSunday)
                .ObserveOn(RxSchedulers.TaskpoolScheduler)
                .Subscribe(_ => RebuildRecurrenceRule());

            m_ReviseRecurrencePattern03Sub = this
                .WhenAnyValue(
                    x => x.Count,
                    x => x.IsMonthDay,
                    x => x.IsMonthWeekday,
                    x => x.ByMonthDay,
                    x => x.ByMonthSetPosSelection,
                    x => x.ByMonthWeekdaySelection)
                .ObserveOn(RxSchedulers.TaskpoolScheduler)
                .Subscribe(_ => RebuildRecurrenceRule());

            m_ReviseRecurrencePattern04Sub = this
                .WhenAnyValue(
                    x => x.IsYearlyMonthDay,
                    x => x.IsYearlyMonthWeekday,
                    x => x.YearlyDayOfMonth,
                    x => x.YearlySetPosSelection,
                    x => x.YearlyWeekdaySelection,
                    x => x.YearlyMonthSelection)
                .ObserveOn(RxSchedulers.TaskpoolScheduler)
                .Subscribe(_ => RebuildRecurrenceRule());

            m_RecurrenceRule = managedHolidayViewModel.RecurrenceRule ?? new RecurrenceRuleModel();
            LoadFromRecurrenceRule();
        }

        #endregion

        #region Properties

        ////// WKST (week start) as two-letter code, e.g. "MO"
        ////public string WeekStart { get; set; } = "MO";

        ////    // RSCALE/SKIP extension hooks (RFC 7529)
        ////    [Reactive] public string? RScale { get; set; }
        ////    [Reactive] public string? Skip { get; set; }

        #endregion

        #region Private Members

        private static List<SetByIntModel> DefaultByMonthSetPosOptions()
        {
            return [
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_BySetPosFirst,
                    Content = 1,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_BySetPosSecond,
                    Content = 2,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_BySetPosThird,
                    Content = 3,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_BySetPosFourth,
                    Content = 4,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_BySetPosLast,
                    Content = -1,
                },
            ];
        }

        private static List<SetByStringModel> DefaultByMonthWeekdayOptions()
        {
            return [
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_WeekdayMonday,
                    Content = RecurrencePatternHelper.DayMondayToken,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_WeekdayTuesday,
                    Content = RecurrencePatternHelper.DayTuesdayToken,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_WeekdayWednesday,
                    Content = RecurrencePatternHelper.DayWednesdayToken,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_WeekdayThursday,
                    Content = RecurrencePatternHelper.DayThursdayToken,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_WeekdayFriday,
                    Content = RecurrencePatternHelper.DayFridayToken,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_WeekdaySaturday,
                    Content = RecurrencePatternHelper.DaySaturdayToken,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_WeekdaySunday,
                    Content = RecurrencePatternHelper.DaySundayToken,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_WeekdayWeekdays,
                    Content = $@"{RecurrencePatternHelper.DayMondayToken},{RecurrencePatternHelper.DayTuesdayToken},{RecurrencePatternHelper.DayWednesdayToken},{RecurrencePatternHelper.DayThursdayToken},{RecurrencePatternHelper.DayFridayToken}",
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_WeekdayWeekends,
                    Content = $@"{RecurrencePatternHelper.DaySaturdayToken},{RecurrencePatternHelper.DaySundayToken}",
                }
            ];
        }

        private static List<SetByIntModel> DefaultYearlySetPosOptions()
        {
            return [
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_BySetPosFirst,
                    Content = 1,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_BySetPosSecond,
                    Content = 2,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_BySetPosThird,
                    Content = 3,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_BySetPosFourth,
                    Content = 4,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_BySetPosLast,
                    Content = -1,
                },
            ];
        }

        private static List<SetByStringModel> DefaultYearlyWeekdayOptions()
        {
            return [
                new SetByStringModel
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_WeekdayMonday,
                    Content = RecurrencePatternHelper.DayMondayToken,
                },
                new SetByStringModel
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_WeekdayTuesday,
                    Content = RecurrencePatternHelper.DayTuesdayToken,
                },
                new SetByStringModel
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_WeekdayWednesday,
                    Content = RecurrencePatternHelper.DayWednesdayToken,
                },
                new SetByStringModel
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_WeekdayThursday,
                    Content = RecurrencePatternHelper.DayThursdayToken,
                },
                new SetByStringModel
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_WeekdayFriday,
                    Content = RecurrencePatternHelper.DayFridayToken,
                },
                new SetByStringModel
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_WeekdaySaturday,
                    Content = RecurrencePatternHelper.DaySaturdayToken,
                },
                new SetByStringModel
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_WeekdaySunday,
                    Content = RecurrencePatternHelper.DaySundayToken,
                },
                new SetByStringModel
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_WeekdayWeekdays,
                    Content = $@"{RecurrencePatternHelper.DayMondayToken},{RecurrencePatternHelper.DayTuesdayToken},{RecurrencePatternHelper.DayWednesdayToken},{RecurrencePatternHelper.DayThursdayToken},{RecurrencePatternHelper.DayFridayToken}",
                },
                new SetByStringModel
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_WeekdayWeekends,
                    Content = $@"{RecurrencePatternHelper.DaySaturdayToken},{RecurrencePatternHelper.DaySundayToken}",
                }
            ];
        }

        private static List<SetByIntModel> DefaultYearlyMonthOptions()
        {
            return [
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_MonthJanuary,
                    Content = 1,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_MonthFebruary,
                    Content = 2,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_MonthMarch,
                    Content = 3,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_MonthApril,
                    Content = 4,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_MonthMay,
                    Content = 5,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_MonthJune,
                    Content = 6,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_MonthJuly,
                    Content = 7,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_MonthAugust,
                    Content = 8,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_MonthSeptember,
                    Content = 9,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_MonthOctober,
                    Content = 10,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_MonthNovember,
                    Content = 11,
                },
                new()
                {
                    Name = Resource.ProjectPlan.Holidays.Holiday_MonthDecember,
                    Content = 12,
                },
            ];
        }

        private SetByIntModel? ByMonthSetPositionToModel(int position)
        {
            return ByMonthSetPosOptions.FirstOrDefault(x => x.Content == position);
        }

        private SetByStringModel? ByMonthWeekdayToModel(string? monthWeekday)
        {
            return ByMonthWeekdayOptions.FirstOrDefault(x => x.Content.Equals(monthWeekday, StringComparison.OrdinalIgnoreCase));
        }

        private SetByIntModel? YearlySetPositionToModel(int position)
        {
            return YearlySetPosOptions.FirstOrDefault(x => x.Content == position);
        }

        private SetByIntModel? YearlyMonthToModel(int month)
        {
            return YearlyMonthOptions.FirstOrDefault(x => x.Content == month);
        }

        private SetByStringModel? YearlyWeekdayToModel(string? monthWeekday)
        {
            return YearlyWeekdayOptions.FirstOrDefault(x => x.Content.Equals(monthWeekday, StringComparison.OrdinalIgnoreCase));
        }

        private void ChangeRecurrenceFrequency(RecurrenceFrequency recurrenceFrequency)
        {
            RecurrenceFrequency = recurrenceFrequency;
        }

        // Populate from an existing RecurrenceRule.
        private void LoadFromRecurrenceRule()
        {
            RecurrenceRuleModel recurrenceRule = RecurrenceRule;
            RecurrenceFrequency = recurrenceRule.Frequency;
            Interval = recurrenceRule.Interval <= 0 ? 1 : recurrenceRule.Interval;

            if (recurrenceRule.Count > 0)
            {
                IsEndCount = true;
                Count = recurrenceRule.Count;
            }
            else if (recurrenceRule.Until.HasValue)
            {
                IsEndUntil = true;
                Until = recurrenceRule.Until;
            }
            else
            {
                IsEndNever = true;
            }

            // Weekly
            if (RecurrenceFrequency == RecurrenceFrequency.Weekly)
            {
                HashSet<DayOfWeek> byDays = [.. recurrenceRule.ByDay.Select(RecurrencePatternHelper.ToDayOfWeek)];

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
            if (RecurrenceFrequency == RecurrenceFrequency.Monthly)
            {
                if (recurrenceRule.ByMonthDay.Count != 0)
                {
                    ByMonthDay = recurrenceRule.ByMonthDay.First();
                    IsMonthDay = true;
                }
                else
                {
                    ByMonthDay = null;
                    IsMonthDay = false;
                }

                if (recurrenceRule.BySetPos.Count != 0
                    && recurrenceRule.ByDay.Count != 0)
                {
                    ByMonthSetPosSelection = ByMonthSetPositionToModel(recurrenceRule.BySetPos.FirstOrDefault());
                    string[] wds = [.. recurrenceRule.ByDay.Select(RecurrencePatternHelper.ToDayToken)];
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
            if (RecurrenceFrequency == RecurrenceFrequency.Yearly)
            {
                if (recurrenceRule.ByMonth.Count != 0
                    && recurrenceRule.ByMonthDay.Count != 0)
                {
                    YearlyMonthSelection = YearlyMonthToModel(recurrenceRule.ByMonth.FirstOrDefault());

                    YearlyDayOfMonth = recurrenceRule.ByMonthDay.First();
                    IsYearlyMonthDay = true;
                }
                else
                {
                    YearlyDayOfMonth = null;
                    IsYearlyMonthDay = false;
                }

                if (recurrenceRule.ByMonth.Count != 0
                    && recurrenceRule.BySetPos.Count != 0
                    && recurrenceRule.ByDay.Count != 0)
                {
                    YearlyMonthSelection = YearlyMonthToModel(recurrenceRule.ByMonth.FirstOrDefault());
                    YearlySetPosSelection = YearlySetPositionToModel(recurrenceRule.BySetPos.FirstOrDefault());
                    YearlyWeekdaySelection = YearlyWeekdayToModel(string.Join(",", recurrenceRule.ByDay.Select(RecurrencePatternHelper.ToDayToken)));
                    IsYearlyMonthWeekday = true;
                }
                else
                {
                    IsYearlyMonthWeekday = false;
                    YearlySetPosSelection = null;
                    YearlyWeekdaySelection = null;
                }
            }
        }

        private void RebuildRecurrenceRule()
        {
            RecurrenceFrequency recurrenceFrequency = RecurrenceFrequency;

            if (recurrenceFrequency == RecurrenceFrequency.None)
            {
                RecurrenceRule = new RecurrenceRuleModel();
                return;
            }

            int interval = Interval <= 0 ? 1 : Interval;

            // End conditions

            int? count;
            DateTime? until;

            if (IsEndUntil && Until.HasValue)
            {
                until = Until.Value;
                count = null;
            }
            else if (IsEndCount && Count.HasValue)
            {
                count = Count.Value;
                until = null;
            }
            else
            {
                count = null;
                until = null;
            }

            //// WKST
            //if (!string.IsNullOrWhiteSpace(WeekStart))
            //{
            //    pattern.WeekStart = ParseWeekDay(WeekStart);
            //}

            var recurrenceRule = new RecurrenceRuleModel
            {
                Frequency = recurrenceFrequency,
                Interval = interval,
                Count = count,
                Until = until,
            };

            // BYDAY for weekly
            if (recurrenceFrequency == RecurrenceFrequency.Weekly)
            {
                recurrenceRule.ByDay.Clear();
                if (ByWeekDaysMonday)
                {
                    recurrenceRule.ByDay.Add(RecurrenceDay.MO);
                }
                if (ByWeekDaysTuesday)
                {
                    recurrenceRule.ByDay.Add(RecurrenceDay.TU);
                }
                if (ByWeekDaysWednesday)
                {
                    recurrenceRule.ByDay.Add(RecurrenceDay.WE);
                }
                if (ByWeekDaysThursday)
                {
                    recurrenceRule.ByDay.Add(RecurrenceDay.TH);
                }
                if (ByWeekDaysFriday)
                {
                    recurrenceRule.ByDay.Add(RecurrenceDay.FR);
                }
                if (ByWeekDaysSaturday)
                {
                    recurrenceRule.ByDay.Add(RecurrenceDay.SA);
                }
                if (ByWeekDaysSunday)
                {
                    recurrenceRule.ByDay.Add(RecurrenceDay.SU);
                }
            }

            // MONTHLY
            if (recurrenceFrequency == RecurrenceFrequency.Monthly)
            {
                recurrenceRule.ByMonthDay.Clear();
                recurrenceRule.ByDay.Clear();
                recurrenceRule.BySetPos.Clear();

                // ByMonthDay
                if (ByMonthDay.HasValue
                    && ByMonthDay.GetValueOrDefault() > 0)
                {
                    recurrenceRule.ByMonthDay.Add(ByMonthDay.GetValueOrDefault());
                }

                // Pattern via BySetPosSelection + ByMonthWeekdaySelection
                if (ByMonthSetPosSelection is not null
                    && ByMonthWeekdaySelection is not null)
                {
                    recurrenceRule.BySetPos.Add(ByMonthSetPosSelection.Content);
                    string[] tokens = ByMonthWeekdaySelection.Content.Split(',', StringSplitOptions.RemoveEmptyEntries);

                    foreach (string token in tokens)
                    {
                        RecurrenceDay day = RecurrencePatternHelper.ParseDay(token.Trim());
                        recurrenceRule.ByDay.Add(day);
                    }
                }
            }

            // YEARLY
            if (RecurrenceFrequency == RecurrenceFrequency.Yearly)
            {
                recurrenceRule.ByMonth.Clear();
                recurrenceRule.ByMonthDay.Clear();
                recurrenceRule.ByDay.Clear();
                recurrenceRule.BySetPos.Clear();

                // Specific date
                if (YearlyMonthSelection is not null
                    && YearlyMonthSelection.Content >= 1 && YearlyMonthSelection.Content <= 12
                    && YearlyDayOfMonth.HasValue
                    && YearlyDayOfMonth.GetValueOrDefault() > 0)
                {
                    recurrenceRule.ByMonth.Add(YearlyMonthSelection.Content);
                    recurrenceRule.ByMonthDay.Add(YearlyDayOfMonth.GetValueOrDefault());
                }

                // Pattern via YearlySetPosSelection + YearlyWeekdaySelection + YearlyMonthSelection
                if (YearlyMonthSelection is not null
                    && YearlyMonthSelection.Content >= 1 && YearlyMonthSelection.Content <= 12
                    && YearlySetPosSelection is not null
                    && YearlyWeekdaySelection is not null)
                {
                    recurrenceRule.ByMonth.Clear();
                    recurrenceRule.ByMonth.Add(YearlyMonthSelection.Content);
                    recurrenceRule.BySetPos.Add(YearlySetPosSelection.Content);
                    string[] tokens = YearlyWeekdaySelection.Content.Split(',', StringSplitOptions.RemoveEmptyEntries);

                    foreach (string token in tokens)
                    {
                        RecurrenceDay day = RecurrencePatternHelper.ParseDay(token.Trim());
                        recurrenceRule.ByDay.Add(day);
                    }
                }
            }

            //if (!string.IsNullOrEmpty(RScale))
            //{
            //    RecurrenceRule += $";RSCALE={RScale}";
            //}
            //if (!string.IsNullOrEmpty(Skip))
            //{
            //    RecurrenceRule += $";SKIP={Skip}";
            //}

            // Check to make sure we don't create a complex rule
            // if the simple "every day" option is selected,
            // e.g. FREQ=DAILY;INTERVAL=1 with no end date or count.
            if (RecurrenceRuleHelper.IsRecurrenceRuleEveryDay(recurrenceRule))
            {
                RecurrenceRule = new RecurrenceRuleModel();
                return;
            }

            RecurrenceRule = recurrenceRule;
        }

        #endregion

        #region IHolidayEditViewModel Members

        private DateTimeOffset? m_StartDateTime;
        public DateTime? StartDateTime
        {
            get => m_StartDateTime?.DateTime;
            set
            {
                // Convert to local now using TimeProvider as we do not know
                // if the input is provided as just a datetime from XAML.
                DateTimeOffset? input = value is null ? null : m_DateTimeCalculator.GetLocal(value.Value);
                this.RaiseAndSetIfChanged(ref m_StartDateTime, input);
            }
        }

        private RecurrenceFrequency m_RecurrenceFrequency;
        public RecurrenceFrequency RecurrenceFrequency
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

        private DateTimeOffset? m_Until;
        public DateTime? Until
        {
            get => m_Until?.DateTime;
            set
            {
                // Convert to local now using TimeProvider as we do not know
                // if the input is provided as just a datetime from XAML.
                DateTimeOffset? input = value is null ? null : m_DateTimeCalculator.GetLocal(value.Value);
                this.RaiseAndSetIfChanged(ref m_Until, input);
            }
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


        private RecurrenceRuleModel m_RecurrenceRule;
        public RecurrenceRuleModel RecurrenceRule
        {
            get => m_RecurrenceRule;
            set
            {
                m_RecurrenceRule = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(RecurrencePattern));
                this.RaisePropertyChanged(nameof(RecurrencePatternDisplay));
            }
        }

        public string RecurrencePattern
        {
            get => RecurrencePatternHelper.ToPattern(RecurrenceRule);
        }

        public string RecurrencePatternDisplay
        {
            get => RecurrenceRuleHelper.ToPhrase(RecurrenceRule);
        }

        public ICommand ChangeRecurrenceFrequencyCommand { get; }

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
