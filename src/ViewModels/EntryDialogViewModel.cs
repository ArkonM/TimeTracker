using System.ComponentModel;
using System.Runtime.CompilerServices;
using TimeTracker.Helpers;
using TimeTracker.Models;

namespace TimeTracker.ViewModels;

public class EntryDialogViewModel : INotifyPropertyChanged
{
    private string _startText = "";
    private string _endText = "";
    private int _breakMinutes;
    private bool _isVacation;
    private bool _isSick;
    private bool _isHomeOffice;

    public DateOnly Date { get; set; }
    public string DateDisplay => Date.ToString("dddd, dd MMMM yyyy");

    public bool IsVacation
    {
        get => _isVacation;
        set
        {
            _isVacation = value;
            if (value) _isSick = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSick));
            OnPropertyChanged(nameof(IsWorkEntry));
            OnPropertyChanged(nameof(IsValid));
        }
    }

    public bool IsSick
    {
        get => _isSick;
        set
        {
            _isSick = value;
            if (value) _isVacation = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsVacation));
            OnPropertyChanged(nameof(IsWorkEntry));
            OnPropertyChanged(nameof(IsValid));
        }
    }

    public bool IsWorkEntry => !_isVacation && !_isSick;

    public bool IsHomeOffice
    {
        get => _isHomeOffice;
        set { _isHomeOffice = value; OnPropertyChanged(); }
    }

    public double? VacationBalance { get; set; }
    public string VacationBalanceDisplay => VacationBalance.HasValue ? $"({VacationBalance.Value:F1} d remaining)" : "";

    public EntryType EntryType => _isVacation ? EntryType.Vacation : _isSick ? EntryType.Sick : EntryType.Work;

    public string StartText
    {
        get => _startText;
        set { _startText = value; OnPropertyChanged(); OnPropertyChanged(nameof(WorkDisplay)); OnPropertyChanged(nameof(IsValid)); }
    }

    public string EndText
    {
        get => _endText;
        set { _endText = value; OnPropertyChanged(); OnPropertyChanged(nameof(WorkDisplay)); OnPropertyChanged(nameof(IsValid)); }
    }

    public int BreakMinutes
    {
        get => _breakMinutes;
        set { _breakMinutes = value; OnPropertyChanged(); OnPropertyChanged(nameof(WorkDisplay)); }
    }

    public string WorkDisplay
    {
        get
        {
            if (!IsWorkEntry) return "";
            if (TryParse(out var s, out var e))
            {
                var work = e - s - TimeSpan.FromMinutes(BreakMinutes);
                return work > TimeSpan.Zero ? DurationFormatter.Format(work) : "—";
            }
            return "—";
        }
    }

    public bool IsValid => !IsWorkEntry || TryParse(out _, out _);

    public TimeSpan StartTime => TimeSpan.TryParse(StartText, out var s) ? s : TimeSpan.Zero;
    public TimeSpan EndTime => TimeSpan.TryParse(EndText, out var e) ? e : TimeSpan.Zero;
    public TimeSpan BreakDuration => IsWorkEntry ? TimeSpan.FromMinutes(BreakMinutes) : TimeSpan.Zero;

    public void LoadFromEntry(TimeEntry entry)
    {
        Date = entry.Date;
        IsHomeOffice = entry.IsHomeOffice;
        if (entry.EntryType == EntryType.Vacation)
            IsVacation = true;
        else if (entry.EntryType == EntryType.Sick)
            IsSick = true;
        else
        {
            StartText = DurationFormatter.FormatTime(entry.StartTime);
            EndText = DurationFormatter.FormatTime(entry.EndTime);
            BreakMinutes = (int)entry.BreakDuration.TotalMinutes;
        }
    }

    private bool TryParse(out TimeSpan start, out TimeSpan end)
    {
        start = end = TimeSpan.Zero;
        return TimeSpan.TryParse(StartText, out start) && TimeSpan.TryParse(EndText, out end) && end > start;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
