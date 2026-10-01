using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimeTracker.Data;
using TimeTracker.Helpers;
using TimeTracker.Models;
using TimeTracker.Services;

namespace TimeTracker.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly TimeEntryService _service;
    private readonly HolidayService _holidays;
    private readonly VacationService? _vacationService;
    private readonly DispatcherTimer _timer;

    private int _year;
    private int _month;
    private DateTime? _sessionStart;
    private TimeSpan _completedWorkToday;
    private TimeSpan _todayEffectiveStart;

    [ObservableProperty] private string _monthTitle = "";
    [ObservableProperty] private string _monthlySummary = "";
    [ObservableProperty] private string _monthlyBalanceDisplay = "";
    [ObservableProperty] private bool _isMonthlyBalanceNegative;
    [ObservableProperty] private string _totalOvertimeDisplay = "";
    [ObservableProperty] private bool _isTotalOvertimeNegative;
    [ObservableProperty] private string _elapsedDisplay = "00:00:00";
    [ObservableProperty] private string _balanceTodayDisplay = "";    // -7:42 → 0:00 → +overtime
    [ObservableProperty] private bool _isTodayDeficit;
    [ObservableProperty] private bool _isTodayOvertime;
    [ObservableProperty] private bool _isTracking;
    [ObservableProperty] private bool _canNavigateNext;
    [ObservableProperty] private string _vacationBalanceDisplay = "";
    [ObservableProperty] private bool _hasVacationConfig;

    public ObservableCollection<DayRowViewModel> DayRows { get; } = new();

    private AppSettings Settings => _service.Settings;

    public MainViewModel()
    {
        _service = new TimeEntryService();
        _holidays = new HolidayService();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;

        var vacation = new VacationService(Settings);
        if (vacation.IsConfigured)
        {
            _vacationService = vacation;
            HasVacationConfig = true;
        }

        _year = DateTime.Today.Year;
        _month = DateTime.Today.Month;

        LoadMonth();
        ResumeSession();
    }

    private void LoadMonth()
    {
        MonthTitle = new DateTime(_year, _month, 1).ToString("MMMM yyyy");
        CanNavigateNext = new DateTime(_year, _month, 1) < new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(12);

        var firstOfMonth = new DateTime(_year, _month, 1);
        var lastOfMonth = new DateTime(_year, _month, DateTime.DaysInMonth(_year, _month));

        int daysFromMonday = firstOfMonth.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)firstOfMonth.DayOfWeek - 1;
        var rangeStart = DateOnly.FromDateTime(firstOfMonth.AddDays(-daysFromMonday));

        int daysToSunday = lastOfMonth.DayOfWeek == DayOfWeek.Sunday ? 0 : 7 - (int)lastOfMonth.DayOfWeek;
        var rangeEnd = DateOnly.FromDateTime(lastOfMonth.AddDays(daysToSunday));

        var entryMap = _service.GetEntriesForMonth(_year, _month)
            .GroupBy(e => e.Date)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.CreatedAt).First());

        DayRows.Clear();
        for (var date = rangeStart; date <= rangeEnd; date = date.AddDays(1))
        {
            bool isOutOfMonth = date.Month != _month || date.Year != _year;
            var row = new DayRowViewModel(date, _holidays.IsHoliday(date),
                _holidays.GetHolidayName(date), Settings, isOutOfMonth, EditEntry, DeleteEntry);
            if (!isOutOfMonth && entryMap.TryGetValue(date, out var entry))
                row.SetEntry(entry);
            DayRows.Add(row);
        }

        UpdateSummary();
    }

    private void ResumeSession()
    {
        var session = _service.ActiveSession;
        if (session == null) return;

        if (session.StartedAt.Date < DateTime.Today)
        {
            AutoStop(session);
            return;
        }

        _sessionStart = session.StartedAt;
        InitTodaySessionState();
        IsTracking = true;
        _timer.Start();
        UpdateTodayLive();
    }

    private void AutoStop(ActiveSession session)
    {
        var ws = new WorkSession { Start = session.StartedAt.TimeOfDay, End = new TimeSpan(23, 59, 0) };
        var existing = _service.GetEntryForDate(DateOnly.FromDateTime(session.StartedAt.Date));
        if (existing != null)
        {
            MergeSession(existing, ws);
            existing.AutoStopped = true;
            _service.UpdateEntry(existing);
        }
        else
        {
            _service.AddEntry(new TimeEntry
            {
                Date = DateOnly.FromDateTime(session.StartedAt.Date),
                StartTime = ws.Start,
                EndTime = ws.End,
                BreakDuration = TimeSpan.Zero,
                AutoStopped = true,
                Sessions = [ws]
            });
        }
        _service.ClearSession();
    }

    private void InitTodaySessionState()
    {
        var todayEntry = _service.GetEntryForDate(DateOnly.FromDateTime(DateTime.Today));
        if (todayEntry is { EntryType: EntryType.Work } && todayEntry.WorkDuration > TimeSpan.Zero)
        {
            _completedWorkToday = todayEntry.WorkDuration;
            _todayEffectiveStart = todayEntry.StartTime < _sessionStart!.Value.TimeOfDay
                ? todayEntry.StartTime
                : _sessionStart.Value.TimeOfDay;
        }
        else
        {
            _completedWorkToday = TimeSpan.Zero;
            _todayEffectiveStart = _sessionStart!.Value.TimeOfDay;
        }
    }

    // Merges a finished timer session into an existing entry. Manual entries carry their
    // times only in the flat fields (Sessions is empty), so that block is first converted
    // into a session to keep it; the gap between blocks becomes break time.
    private static void MergeSession(TimeEntry entry, WorkSession ws)
    {
        var previousWork = entry.WorkDuration;
        if (entry.Sessions.Count == 0 && entry.EndTime > entry.StartTime)
            entry.Sessions.Add(new WorkSession { Start = entry.StartTime, End = entry.EndTime });

        entry.Sessions.Add(ws);
        entry.StartTime = entry.Sessions.Min(s => s.Start);
        entry.EndTime = entry.Sessions.Max(s => s.End);

        var totalWork = previousWork + (ws.End - ws.Start);
        var span = entry.EndTime - entry.StartTime;
        entry.BreakDuration = span > totalWork ? span - totalWork : TimeSpan.Zero;
    }

    [RelayCommand]
    private void Start()
    {
        _service.StartSession();
        _sessionStart = _service.ActiveSession!.StartedAt;
        InitTodaySessionState();
        IsTracking = true;
        _timer.Start();

        if (_year == DateTime.Today.Year && _month == DateTime.Today.Month)
            UpdateTodayLive();
    }

    [RelayCommand]
    private void Stop()
    {
        _timer.Stop();

        var ws = new WorkSession { Start = _sessionStart!.Value.TimeOfDay, End = DateTime.Now.TimeOfDay };
        var existing = _service.GetEntryForDate(DateOnly.FromDateTime(_sessionStart.Value.Date));
        if (existing != null)
        {
            MergeSession(existing, ws);
            _service.UpdateEntry(existing);
        }
        else
        {
            _service.AddEntry(new TimeEntry
            {
                Date = DateOnly.FromDateTime(_sessionStart.Value.Date),
                StartTime = ws.Start,
                EndTime = ws.End,
                BreakDuration = TimeSpan.Zero,
                Sessions = [ws]
            });
        }
        _service.ClearSession();

        _completedWorkToday = TimeSpan.Zero;
        IsTracking = false;
        _sessionStart = null;
        ElapsedDisplay = "00:00:00";
        BalanceTodayDisplay = "";
        IsTodayDeficit = IsTodayOvertime = false;

        if (_year == DateTime.Today.Year && _month == DateTime.Today.Month)
            LoadMonth();
    }

    [RelayCommand]
    private void GoToCurrentMonth()
    {
        _year = DateTime.Today.Year;
        _month = DateTime.Today.Month;
        LoadMonth();
    }

    [RelayCommand]
    private void PreviousMonth()
    {
        var dt = new DateTime(_year, _month, 1).AddMonths(-1);
        _year = dt.Year; _month = dt.Month;
        LoadMonth();
    }

    [RelayCommand]
    private void NextMonth()
    {
        var dt = new DateTime(_year, _month, 1).AddMonths(1);
        _year = dt.Year; _month = dt.Month;
        LoadMonth();
    }

    [RelayCommand]
    private void Export()
    {
        var dlg = new Views.ExportDialog(_service.GetMonthsWithEntries(), _year, _month);
        if (dlg.ShowDialog() != true) return;

        var save = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx",
            FileName = $"TimeTracker_{dlg.ViewModel.SelectedMonths.First().Year:D4}.xlsx"
        };
        if (save.ShowDialog() != true) return;

        new ExportService(_service, _holidays).Export(dlg.ViewModel.SelectedMonths, save.FileName, dlg.ViewModel.Mode);
        System.Windows.MessageBox.Show("Export completed.", "TimeTracker",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    private void EditEntry(DayRowViewModel row)
    {
        var vacationBalance = _vacationService?.ComputeRemainingDays(
            DateOnly.FromDateTime(DateTime.Today), _service.GetAllEntries());
        var dlg = new Views.EntryDialog(row.Entry, row.Date, vacationBalance);
        if (dlg.ShowDialog() != true) return;

        var vm = dlg.ViewModel;
        if (row.Entry != null)
        {
            row.Entry.EntryType = vm.EntryType;
            row.Entry.StartTime = vm.StartTime;
            row.Entry.EndTime = vm.EndTime;
            row.Entry.BreakDuration = vm.BreakDuration;
            row.Entry.IsHomeOffice = vm.IsHomeOffice;
            row.Entry.Sessions.Clear();
            _service.UpdateEntry(row.Entry);
        }
        else
        {
            _service.AddEntry(new TimeEntry
            {
                Date = row.Date,
                EntryType = vm.EntryType,
                StartTime = vm.StartTime,
                EndTime = vm.EndTime,
                BreakDuration = vm.BreakDuration,
                IsHomeOffice = vm.IsHomeOffice
            });
        }
        LoadMonth();
    }

    private void DeleteEntry(DayRowViewModel row)
    {
        if (row.Entry == null) return;
        var result = System.Windows.MessageBox.Show(
            $"Delete entry for {row.DateLabel} {row.WeekdayLabel}?",
            "Confirm", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
        if (result != System.Windows.MessageBoxResult.Yes) return;
        _service.DeleteEntry(row.Entry.Id, row.Entry.Date);
        LoadMonth();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_sessionStart == null) return;

        if (DateTime.Now.Date > _sessionStart.Value.Date)
        {
            _timer.Stop();
            AutoStop(_service.ActiveSession!);
            IsTracking = false;
            _sessionStart = null;
            ElapsedDisplay = "00:00:00";
            BalanceTodayDisplay = "";
            IsTodayDeficit = IsTodayOvertime = false;
            LoadMonth();
            return;
        }

        var totalWork = _completedWorkToday + (DateTime.Now - _sessionStart.Value);
        ElapsedDisplay = DurationFormatter.FormatElapsed(totalWork);
        UpdateTodayBalance(totalWork);
        UpdateTotalOvertimeLive(totalWork);

        if (_year == DateTime.Today.Year && _month == DateTime.Today.Month)
            UpdateTodayLive();
    }

    private void UpdateTodayLive()
    {
        if (_sessionStart == null) return;
        var today = DayRows.FirstOrDefault(r => r.IsToday);
        if (today == null) return;

        var totalWork = _completedWorkToday + (DateTime.Now - _sessionStart.Value);
        var totalSpan = DateTime.Now.TimeOfDay - _todayEffectiveStart;
        var breakSoFar = totalSpan > totalWork ? totalSpan - totalWork : TimeSpan.Zero;

        today.SetLiveTracking(_todayEffectiveStart, totalWork, breakSoFar);
    }

    private void UpdateTodayBalance(TimeSpan elapsed)
    {
        var balance = elapsed - Settings.DailyTarget;
        BalanceTodayDisplay = balance == TimeSpan.Zero ? "0:00"
            : balance > TimeSpan.Zero ? "+" + DurationFormatter.Format(balance)
            : "-" + DurationFormatter.Format(balance.Duration());

        IsTodayDeficit = balance < TimeSpan.Zero;
        IsTodayOvertime = balance > TimeSpan.Zero;
    }

    private void UpdateTotalOvertimeLive(TimeSpan liveWorkToday)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var allWorkEntries = _service.GetAllEntries().Where(e => e.EntryType == EntryType.Work && e.Date != today);
        var totalOvertime = allWorkEntries.Aggregate(TimeSpan.Zero, (s, e) => s + (e.WorkDuration - Settings.DailyTarget));
        totalOvertime += liveWorkToday - Settings.DailyTarget;
        IsTotalOvertimeNegative = totalOvertime < TimeSpan.Zero;
        TotalOvertimeDisplay = totalOvertime == TimeSpan.Zero ? "0:00"
            : totalOvertime > TimeSpan.Zero ? $"+{DurationFormatter.Format(totalOvertime)}"
            : $"-{DurationFormatter.Format(totalOvertime.Duration())}";
    }

    private void UpdateSummary()
    {
        var entries = _service.GetEntriesForMonth(_year, _month);
        var absenceDays = entries.Count(e => e.EntryType != EntryType.Work);
        var totalWork = entries.Where(e => e.EntryType == EntryType.Work)
            .Aggregate(TimeSpan.Zero, (s, e) => s + e.WorkDuration);
        int days = DateTime.DaysInMonth(_year, _month);
        int workdays = Enumerable.Range(1, days)
            .Select(d => new DateOnly(_year, _month, d))
            .Count(d => d.ToDateTime(TimeOnly.MinValue).DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
                        && !_holidays.IsHoliday(d));
        var expected = Settings.DailyTarget * (workdays - absenceDays);
        var balance = totalWork - expected;
        MonthlySummary = $"Worked: {DurationFormatter.Format(totalWork)}  /  Expected: {DurationFormatter.Format(expected)}  |  ";
        MonthlyBalanceDisplay = balance >= TimeSpan.Zero
            ? $"+{DurationFormatter.Format(balance)}"
            : $"-{DurationFormatter.Format(balance.Duration())}";
        IsMonthlyBalanceNegative = balance < TimeSpan.Zero;

        var allWorkEntries = _service.GetAllEntries().Where(e => e.EntryType == EntryType.Work);
        var totalOvertime = allWorkEntries.Aggregate(TimeSpan.Zero, (s, e) => s + (e.WorkDuration - Settings.DailyTarget));
        IsTotalOvertimeNegative = totalOvertime < TimeSpan.Zero;
        TotalOvertimeDisplay = totalOvertime == TimeSpan.Zero ? "0:00"
            : totalOvertime > TimeSpan.Zero ? $"+{DurationFormatter.Format(totalOvertime)}"
            : $"-{DurationFormatter.Format(totalOvertime.Duration())}";

        if (_vacationService != null)
        {
            var remaining = _vacationService.ComputeRemainingDays(
                DateOnly.FromDateTime(DateTime.Today), _service.GetAllEntries());
            VacationBalanceDisplay = $"{remaining:F1} d";
        }
    }
}
