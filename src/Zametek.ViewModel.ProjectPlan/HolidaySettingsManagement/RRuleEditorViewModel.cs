using Ical.Net;
using Ical.Net.DataTypes;
using ReactiveUI;
using System.Collections.ObjectModel;

namespace Zametek.ViewModel.ProjectPlan
{







    public sealed class RRuleEditorViewModel
        : ViewModelBase
    {
        private FrequencyType _frequency = FrequencyType.Weekly;
        private int _interval = 1;
        private int? _count;
        private DateTimeOffset? _until;
        private DayOfWeek _weekStart = DayOfWeek.Monday;

        public ObservableCollection<DayOfWeek> SelectedByDays { get; } = new();
        public ObservableCollection<int> SelectedByMonthDays { get; } = new();
        public ObservableCollection<int> SelectedByMonths { get; } = new();

        public FrequencyType Frequency
        {
            get => _frequency;
            set { if (_frequency != value) { _frequency = value; this.RaisePropertyChanged(); } }
        }

        public int Interval
        {
            get => _interval;
            set { if (_interval != value) { _interval = Math.Max(1, value); this.RaisePropertyChanged(); } }
        }

        public int? Count
        {
            get => _count;
            set
            {
                if (_count != value)
                {
                    _count = value;
                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged(nameof(ToRRuleString));
                }
            }
        }

        public DateTimeOffset? Until
        {
            get => _until;
            set { if (_until != value) { _until = value; this.RaisePropertyChanged(); } }
        }

        public DayOfWeek WeekStart
        {
            get => _weekStart;
            set { if (_weekStart != value) { _weekStart = value; this.RaisePropertyChanged(); } }
        }

        public Array FrequencyValues => Enum.GetValues<FrequencyType>();
        public Array DayValues => Enum.GetValues<DayOfWeek>();

        public void LoadFromRRule(string? rrule)
        {
            ClearCollections();

            if (string.IsNullOrWhiteSpace(rrule))
                return;

            var pattern = new RecurrencePattern(rrule);

            Frequency = pattern.Frequency;
            Interval = pattern.Interval <= 0 ? 1 : pattern.Interval;
            Count = pattern.Count > 0 ? pattern.Count : null;
            WeekStart = pattern.FirstDayOfWeek;

            if (pattern.Until != null)
                Until = new DateTimeOffset(pattern.Until.Value);//.AsSystemLocal);

            if (pattern.ByDay != null)
            {
                foreach (var day in pattern.ByDay)
                {
                    if (!SelectedByDays.Contains(day.DayOfWeek))
                        SelectedByDays.Add(day.DayOfWeek);
                }
            }

            if (pattern.ByMonthDay != null)
            {
                foreach (var md in pattern.ByMonthDay)
                    if (!SelectedByMonthDays.Contains(md))
                        SelectedByMonthDays.Add(md);
            }

            if (pattern.ByMonth != null)
            {
                foreach (var m in pattern.ByMonth)
                    if (!SelectedByMonths.Contains(m))
                        SelectedByMonths.Add(m);
            }

            this.RaisePropertyChanged(nameof(SelectedByDays));
            this.RaisePropertyChanged(nameof(SelectedByMonthDays));
            this.RaisePropertyChanged(nameof(SelectedByMonths));
        }

        public RecurrencePattern ToRecurrencePattern()
        {
            var pattern = new RecurrencePattern
            {
                Frequency = Frequency,
                Interval = Math.Max(1, Interval),
                FirstDayOfWeek = WeekStart
            };

            if (Count is > 0)
                pattern.Count = Count.Value;

            if (Until.HasValue)
                pattern.Until = new CalDateTime(Until.Value.DateTime);

            if (SelectedByDays.Any())
                pattern.ByDay = SelectedByDays.Select(d => new WeekDay(d)).ToList();

            if (SelectedByMonthDays.Any())
                pattern.ByMonthDay = SelectedByMonthDays.ToList();

            if (SelectedByMonths.Any())
                pattern.ByMonth = SelectedByMonths.ToList();

            return pattern;
        }

        public string ToRRuleString
        {
            get => ToRecurrencePattern().ToString();

        }

        private void ClearCollections()
        {
            SelectedByDays.Clear();
            SelectedByMonthDays.Clear();
            SelectedByMonths.Clear();
        }
    }







    //public enum RecurrenceEndType
    //{
    //    Never,
    //    Count,
    //    Until
    //}

    //public class DayOfMonthItem : INotifyPropertyChanged
    //{
    //    private bool _isSelected;

    //    public int Day { get; }

    //    public bool IsSelected
    //    {
    //        get => _isSelected;
    //        set
    //        {
    //            if (_isSelected == value) return;
    //            _isSelected = value;
    //            OnPropertyChanged();
    //        }
    //    }

    //    public DayOfMonthItem(int day)
    //    {
    //        Day = day;
    //    }

    //    public event PropertyChangedEventHandler? PropertyChanged;
    //    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    //        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    //}

    //public class RRuleEditorViewModel : ViewModelBase, IDataErrorInfo
    //{
    //    private FrequencyType _frequency;
    //    private int _interval;
    //    private RecurrenceEndType _endType;
    //    private int _count;
    //    private DateTime? _until;
    //    private string? _rruleText;
    //    private RecurrencePattern? _pattern;

    //    public FrequencyType Frequency
    //    {
    //        get => _frequency;
    //        set
    //        {
    //            if (_frequency == value) return;
    //            _frequency = value;
    //            OnPropertyChanged();
    //            UpdatePatternFromState();
    //        }
    //    }

    //    public int Interval
    //    {
    //        get => _interval;
    //        set
    //        {
    //            if (_interval == value) return;
    //            _interval = value;
    //            OnPropertyChanged();
    //            UpdatePatternFromState();
    //        }
    //    }

    //    public RecurrenceEndType EndType
    //    {
    //        get => _endType;
    //        set
    //        {
    //            if (_endType == value) return;
    //            _endType = value;
    //            OnPropertyChanged();
    //            UpdatePatternFromState();
    //        }
    //    }

    //    public int Count
    //    {
    //        get => _count;
    //        set
    //        {
    //            if (_count == value) return;
    //            _count = value;
    //            OnPropertyChanged();
    //            UpdatePatternFromState();
    //        }
    //    }

    //    public DateTime? Until
    //    {
    //        get => _until;
    //        set
    //        {
    //            if (_until == value) return;
    //            _until = value;
    //            OnPropertyChanged();
    //            UpdatePatternFromState();
    //        }
    //    }

    //    public ObservableCollection<DayOfMonthItem> MonthDays { get; } =
    //        new ObservableCollection<DayOfMonthItem>(
    //            Enumerable.Range(1, 31).Select(d => new DayOfMonthItem(d)));

    //    public string? RRuleText
    //    {
    //        get => _rruleText;
    //        private set
    //        {
    //            if (_rruleText == value) return;
    //            _rruleText = value;
    //            OnPropertyChanged();
    //        }
    //    }

    //    public RecurrencePattern? Pattern
    //    {
    //        get => _pattern;
    //        private set
    //        {
    //            if (_pattern == value) return;
    //            _pattern = value;
    //            OnPropertyChanged();
    //        }
    //    }

    //    public ICommand ApplyCommand { get; }
    //    public ICommand ResetCommand { get; }

    //    public RRuleEditorViewModel()
    //    {
    //        _frequency = FrequencyType.Weekly;
    //        _interval = 1;
    //        _endType = RecurrenceEndType.Never;
    //        _count = 10;
    //        _until = null;

    //        ApplyCommand = new RelayCommand(_ => UpdatePatternFromState(), _ => CanApply());
    //        ResetCommand = new RelayCommand(_ => Reset(), _ => true);

    //        // Default: no BYMONTHDAY selection; callers can set.
    //        UpdatePatternFromState();
    //    }

    //    public void LoadFromPattern(RecurrencePattern pattern)
    //    {
    //        _pattern = pattern;

    //        Frequency = pattern.Frequency;
    //        Interval = pattern.Interval <= 0 ? 1 : pattern.Interval;

    //        if (pattern.Count > 0)
    //        {
    //            EndType = RecurrenceEndType.Count;
    //            Count = pattern.Count.GetValueOrDefault();
    //            Until = null;
    //        }
    //        else if (pattern.Until != new CalDateTime(DateTime.MinValue) && pattern.Until != default)
    //        {
    //            EndType = RecurrenceEndType.Until;
    //            Until = pattern.Until.AsUtc; // TODO
    //            Count = 0;
    //        }
    //        else
    //        {
    //            EndType = RecurrenceEndType.Never;
    //            Count = 0;
    //            Until = null;
    //        }

    //        foreach (var item in MonthDays)
    //            item.IsSelected = false;

    //        if (pattern.ByMonthDay != null)
    //        {
    //            foreach (var d in pattern.ByMonthDay)
    //            {
    //                var item = MonthDays.FirstOrDefault(m => m.Day == d);
    //                if (item != null)
    //                    item.IsSelected = true;
    //            }
    //        }

    //        RRuleText = pattern.ToString();
    //    }

    //    private bool CanApply()
    //    {
    //        if (Interval <= 0) return false;
    //        if (EndType == RecurrenceEndType.Count && Count <= 0) return false;
    //        if (EndType == RecurrenceEndType.Until && Until == null) return false;
    //        if (Frequency == FrequencyType.Monthly &&
    //            !MonthDays.Any(m => m.IsSelected))
    //            return false;

    //        return true;
    //    }

    //    private void UpdatePatternFromState()
    //    {
    //        if (!CanApply())
    //        {
    //            Pattern = null;
    //            RRuleText = null;
    //            return;
    //        }

    //        var pattern = new RecurrencePattern
    //        {
    //            Frequency = Frequency,
    //            Interval = Interval
    //        };

    //        switch (EndType)
    //        {
    //            case RecurrenceEndType.Never:
    //                pattern.Count = 0;
    //                pattern.Until = new CalDateTime(DateTime.MinValue);
    //                break;
    //            case RecurrenceEndType.Count:
    //                pattern.Count = Count;
    //                pattern.Until = new CalDateTime(DateTime.MinValue);
    //                break;
    //            case RecurrenceEndType.Until:
    //                pattern.Count = 0;
    //                pattern.Until = new CalDateTime(Until ?? DateTime.MinValue);
    //                break;
    //        }

    //        var selectedDays = MonthDays.Where(m => m.IsSelected)
    //                                    .Select(m => m.Day)
    //                                    .ToList();
    //        pattern.ByMonthDay.Clear();
    //        foreach (var d in selectedDays)
    //            pattern.ByMonthDay.Add(d);

    //        Pattern = pattern;
    //        RRuleText = pattern.ToString();
    //    }

    //    private void Reset()
    //    {
    //        Frequency = FrequencyType.Weekly;
    //        Interval = 1;
    //        EndType = RecurrenceEndType.Never;
    //        Count = 10;
    //        Until = null;

    //        foreach (var item in MonthDays)
    //            item.IsSelected = false;

    //        UpdatePatternFromState();
    //    }

    //    #region IDataErrorInfo

    //    public string Error => string.Empty;

    //    public string this[string columnName]
    //    {
    //        get
    //        {
    //            switch (columnName)
    //            {
    //                case nameof(Interval):
    //                    return Interval <= 0 ? "Interval must be greater than 0." : string.Empty;
    //                case nameof(Count):
    //                    if (EndType == RecurrenceEndType.Count && Count <= 0)
    //                        return "Count must be greater than 0.";
    //                    break;
    //                case nameof(Until):
    //                    if (EndType == RecurrenceEndType.Until && Until == null)
    //                        return "Until date is required.";
    //                    break;
    //            }

    //            return string.Empty;
    //        }
    //    }

    //    #endregion

    //    public event PropertyChangedEventHandler? PropertyChanged;
    //    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    //        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    //}

    //public class RelayCommand : ICommand
    //{
    //    private readonly Action<object?> _execute;
    //    private readonly Func<object?, bool>? _canExecute;
    //    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    //    {
    //        _execute = execute;
    //        _canExecute = canExecute;
    //    }
    //    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    //    public void Execute(object? parameter) => _execute(parameter);
    //    public event EventHandler? CanExecuteChanged;
    //    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    //}

























}
