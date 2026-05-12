using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimeTracker.Helpers;
using TimeTracker.Models;

namespace TimeTracker.ViewModels;

public partial class DayRowViewModel : ObservableObject
{
    private readonly Action<DayRowViewModel> _onEdit;
    private readonly Action<DayRowViewModel> _onDelete;

    public DateOnly Date { get; }
    public bool IsWeekend { get; }
    public bool IsHoliday { get; }
    public string? HolidayName { get; }
    public bool IsToday { get; }
    public bool IsWeekStart { get; }
    public bool IsOutOfMonth { get; }
    public string DateLabel { get; }
    public string WeekdayLabel { get; }
    public TimeSpan DailyTarget { get; }
    public TimeSpan OvertimeThreshold { get; }

    [ObservableProperty] private string _startDisplay = "";
    [ObservableProperty] private string _endDisplay = "";
    [ObservableProperty] private string _breakDisplay = "";
    [ObservableProperty] private string _workDisplay = "";
    [ObservableProperty] private string _balanceDisplay = "";  // -7:42 → 0:00 → +overtime
    [ObservableProperty] private bool _isDeficit;              // worked < DailyTarget
    [ObservableProperty] private bool _isActualOvertime;       // worked >= OvertimeThreshold (8h)
    [ObservableProperty] private bool _hasEntry;
    [ObservableProperty] private bool _isActiveDay;

    public TimeEntry? Entry { get; private set; }

    public DayRowViewModel(DateOnly date, bool isHoliday, string? holidayName,
        AppSettings settings, bool isOutOfMonth, Action<DayRowViewModel> onEdit, Action<DayRowViewModel> onDelete)
    {
        Date = date;
        DailyTarget = settings.DailyTarget;
        OvertimeThreshold = settings.OvertimeThreshold;
        IsHoliday = isHoliday;
        HolidayName = holidayName;
        IsOutOfMonth = isOutOfMonth;
        _onEdit = onEdit;
        _onDelete = onDelete;

        var dt = date.ToDateTime(TimeOnly.MinValue);
        IsWeekend = dt.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        IsToday = date == DateOnly.FromDateTime(DateTime.Today);
        IsWeekStart = dt.DayOfWeek == DayOfWeek.Monday;
        DateLabel = date.Day.ToString("D2");
        WeekdayLabel = dt.ToString("ddd");
    }

    public void SetEntry(TimeEntry? entry)
    {
        Entry = entry;
        HasEntry = entry != null;
        IsActiveDay = false;

        if (entry != null)
        {
            if (entry.EntryType == EntryType.Vacation)
            {
                StartDisplay = "Vacation";
                EndDisplay = BreakDisplay = WorkDisplay = BalanceDisplay = "";
                IsDeficit = IsActualOvertime = false;
            }
            else if (entry.EntryType == EntryType.Sick)
            {
                StartDisplay = "Sick Day";
                EndDisplay = BreakDisplay = WorkDisplay = BalanceDisplay = "";
                IsDeficit = IsActualOvertime = false;
            }
            else
            {
                StartDisplay = DurationFormatter.FormatTime(entry.StartTime);
                EndDisplay = DurationFormatter.FormatTime(entry.EndTime);
                BreakDisplay = DurationFormatter.Format(entry.BreakDuration);
                WorkDisplay = DurationFormatter.Format(entry.WorkDuration);
                ApplyBalance(entry.WorkDuration);
            }
        }
        else
        {
            StartDisplay = EndDisplay = BreakDisplay = WorkDisplay = BalanceDisplay = "";
            IsDeficit = IsActualOvertime = false;
        }
    }

    public void SetLiveTracking(TimeSpan startTime, TimeSpan elapsed)
    {
        IsActiveDay = true;
        StartDisplay = DurationFormatter.FormatTime(startTime);
        EndDisplay = "";
        BreakDisplay = "";
        WorkDisplay = DurationFormatter.FormatElapsed(elapsed);
        ApplyBalance(elapsed);
    }

    private void ApplyBalance(TimeSpan worked)
    {
        var balance = worked - DailyTarget;
        BalanceDisplay = balance == TimeSpan.Zero ? "0:00"
            : balance > TimeSpan.Zero ? "+" + DurationFormatter.Format(balance)
            : "-" + DurationFormatter.Format(balance.Duration());

        IsDeficit = balance < TimeSpan.Zero;
        IsActualOvertime = worked >= OvertimeThreshold;
    }

    [RelayCommand] private void Edit() { if (!IsOutOfMonth) _onEdit(this); }
    [RelayCommand] private void Delete() { if (!IsOutOfMonth) _onDelete(this); }
}
