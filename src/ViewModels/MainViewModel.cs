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

    [ObservableProperty] private string _monthTitle = "";
    [ObservableProperty] private string _monthlySummary = "";
    [ObservableProperty] private string _monthlyBalanceDisplay = "";
    [ObservableProperty] private bool _isMonthlyBalanceNegative;
    [ObservableProperty] private string _totalOvertimeDisplay = "";
    [ObservableProperty] private bool _isTotalOvertimeNegative;
    [ObservableProperty] private string _elapsedDisplay = "00:00:00";
    [ObservableProperty] private string _balanceTodayDisplay = "";    // -7:42 → 0:00 → +overtime
    [ObservableProperty] private bool _isTodayDeficit;
    [ObservableProperty] private bool _isTodayActualOvertime;
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

        var vc = new JsonDataStore().LoadVacationConfig();
        if (vc.VacationDaysPerYear > 0)
        {
            _vacationService = new VacationService(vc);
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
        IsTracking = true;
        _timer.Start();
        UpdateTodayLive();
    }

    private void AutoStop(ActiveSession session)
    {
        var breakDuration = TimeSpan.FromMinutes(Settings.DefaultBreakMinutes);
        _service.AddEntry(new TimeEntry
        {
            Date = DateOnly.FromDateTime(session.StartedAt),
            StartTime = session.StartedAt.TimeOfDay,
            EndTime = new TimeSpan(23, 59, 0) + breakDuration,
            BreakDuration = breakDuration,
            AutoStopped = true
        });
        _service.ClearSession();
    }

    [RelayCommand]
    private void Start()
    {
        _service.StartSession();
        _sessionStart = _service.ActiveSession!.StartedAt;
        IsTracking = true;
        _timer.Start();

        if (_year == DateTime.Today.Year && _month == DateTime.Today.Month)
            UpdateTodayLive();
    }

    [RelayCommand]
    private void Stop()
    {
        _timer.Stop();

        var dialog = new Views.BreakDialog(Settings.DefaultBreakMinutes);
        if (dialog.ShowDialog() != true) { _timer.Start(); return; }

        var vm = dialog.ViewModel;
        var breakDuration = TimeSpan.FromMinutes(vm.BreakMinutes);
        var endTime = vm.BreakAlreadyTaken
            ? DateTime.Now.TimeOfDay
            : DateTime.Now.TimeOfDay + breakDuration;

        _service.AddEntry(new TimeEntry
        {
            Date = DateOnly.FromDateTime(_sessionStart!.Value),
            StartTime = _sessionStart.Value.TimeOfDay,
            EndTime = endTime,
            BreakDuration = breakDuration
        });
        _service.ClearSession();

        IsTracking = false;
        _sessionStart = null;
        ElapsedDisplay = "00:00:00";
        BalanceTodayDisplay = "";
        IsTodayDeficit = IsTodayActualOvertime = false;

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
        var dlg = new Views.ExportDialog(_year, _month);
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
                BreakDuration = vm.BreakDuration
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
            IsTodayDeficit = IsTodayActualOvertime = false;
            LoadMonth();
            return;
        }

        var elapsed = DateTime.Now - _sessionStart.Value;
        ElapsedDisplay = DurationFormatter.FormatElapsed(elapsed);
        UpdateTodayBalance(elapsed);

        if (_year == DateTime.Today.Year && _month == DateTime.Today.Month)
            UpdateTodayLive();
    }

    private void UpdateTodayLive()
    {
        if (_sessionStart == null) return;
        var today = DayRows.FirstOrDefault(r => r.IsToday);
        if (today == null) return;
        today.SetLiveTracking(_sessionStart.Value.TimeOfDay, DateTime.Now - _sessionStart.Value);
    }

    private void UpdateTodayBalance(TimeSpan elapsed)
    {
        var balance = elapsed - Settings.DailyTarget;
        BalanceTodayDisplay = balance == TimeSpan.Zero ? "0:00"
            : balance > TimeSpan.Zero ? "+" + DurationFormatter.Format(balance)
            : "-" + DurationFormatter.Format(balance.Duration());

        IsTodayDeficit = balance < TimeSpan.Zero;
        IsTodayActualOvertime = elapsed >= Settings.OvertimeThreshold;
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
