using TimeTracker.Models;

namespace TimeTracker.Services;

public class VacationService
{
    private readonly AppSettings _settings;

    public VacationService(AppSettings settings) => _settings = settings;

    public bool IsConfigured => _settings.EmploymentStartDate.HasValue && _settings.VacationDaysPerYear > 0;

    /// <summary>
    /// Computes total vacation days accrued from EmploymentStartDate up to and including today.
    ///
    /// First year (EmploymentStartDate → first anniversary):
    ///   - VacationDaysPerYear / 12 granted on EmploymentStartDate and on the same day of each following month
    ///
    /// From the first anniversary onwards:
    ///   - Full VacationDaysPerYear granted on each anniversary, nothing in between
    ///
    /// Unused days carry over (never reset).
    /// </summary>
    public double ComputeAccruedDays(DateOnly today)
    {
        if (_settings.EmploymentStartDate is not { } entry || today < entry) return 0.0;

        double monthlyRate = _settings.VacationDaysPerYear / 12.0;
        double total = 0.0;

        for (int m = 0; m < 12 && entry.AddMonths(m) <= today; m++)
            total += monthlyRate;

        for (int y = 1; entry.AddYears(y) <= today; y++)
            total += _settings.VacationDaysPerYear;

        return total;
    }

    public static double ComputeUsedDays(IEnumerable<TimeEntry> allEntries)
        => allEntries.Count(e => e.EntryType == EntryType.Vacation);

    public double ComputeRemainingDays(DateOnly today, IEnumerable<TimeEntry> allEntries)
        => ComputeAccruedDays(today) - ComputeUsedDays(allEntries);
}
