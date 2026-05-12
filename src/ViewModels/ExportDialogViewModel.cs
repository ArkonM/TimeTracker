using System.ComponentModel;
using System.Runtime.CompilerServices;
using TimeTracker.Services;

namespace TimeTracker.ViewModels;

public class MonthOption : INotifyPropertyChanged
{
    private bool _isSelected;
    public int Year { get; init; }
    public int Month { get; init; }
    public string Label { get; init; } = "";

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class ExportDialogViewModel : INotifyPropertyChanged
{
    public List<MonthOption> Months { get; } = new();

    private bool _isCorrected;

    public bool IsActual
    {
        get => !_isCorrected;
        set { if (value && _isCorrected) { _isCorrected = false; OnPropertyChanged(); OnPropertyChanged(nameof(IsCorrected)); } }
    }

    public bool IsCorrected
    {
        get => _isCorrected;
        set { if (value != _isCorrected) { _isCorrected = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsActual)); } }
    }

    public ExportMode Mode => _isCorrected ? ExportMode.Corrected : ExportMode.Actual;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public ExportDialogViewModel(int currentYear, int currentMonth)
    {
        var dt = new DateTime(currentYear, currentMonth, 1);
        for (int i = 0; i < 13; i++)
        {
            var m = dt.AddMonths(-i);
            Months.Add(new MonthOption
            {
                Year = m.Year,
                Month = m.Month,
                Label = m.ToString("MMMM yyyy"),
                IsSelected = i == 0
            });
        }
    }

    public IEnumerable<(int Year, int Month)> SelectedMonths
        => Months.Where(m => m.IsSelected)
                 .OrderBy(m => m.Year).ThenBy(m => m.Month)
                 .Select(m => (m.Year, m.Month));
}
