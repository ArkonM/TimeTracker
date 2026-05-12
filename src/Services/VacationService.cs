using TimeTracker.Models;

namespace TimeTracker.Services;

public class VacationService
{
    private readonly VacationConfig _config;

    public VacationService(VacationConfig config) => _config = config;

    public bool IsConfigured => _config.VacationDaysPerYear > 0;

    /// <summary>
    /// Computes total vacation days accrued from EntryDate up to and including today.
    ///
    /// Monthly phase (EntryDate → first VacationResetDate):
    ///   - On EntryDate: 2 × monthly rate if day == 1, else 1 × monthly rate
    ///   - On 1st of each subsequent month: 1 × monthly rate
    ///
    /// Annual phase (from first VacationResetDate onwards):
    ///   - Full VacationDaysPerYear granted on each occurrence of the reset date
    ///
    /// Both phases add to the running total (carry-over, never reset).
    /// </summary>
    public double ComputeAccruedDays(DateOnly today)
    {
        var entry = _config.EntryDate;
        if (today < entry) return 0.0;

        double monthlyRate = _config.VacationDaysPerYear / 12.0;
        var firstReset = FirstResetOnOrAfter(entry);
        double total = 0.0;

        if (entry < firstReset)
        {
            // Initial grant on entry date: full month if starting on the 1st, half month otherwise
            total += entry.Day == 1 ? monthlyRate : monthlyRate / 2.0;

            // Monthly grants on 1st of each subsequent month, up to (exclusive) firstReset
            var grantDate = new DateOnly(entry.Year, entry.Month, 1).AddMonths(1);
            var monthlyEnd = today >= firstReset ? firstReset : today.AddDays(1);
            while (grantDate < monthlyEnd)
            {
                total += monthlyRate;
                grantDate = grantDate.AddMonths(1);
            }
        }

        // Annual grants from firstReset onwards (inclusive)
        var resetDate = firstReset;
        while (resetDate <= today)
        {
            total += _config.VacationDaysPerYear;
            resetDate = new DateOnly(resetDate.Year + 1, _config.VacationResetMonth, _config.VacationResetDay);
        }

        return total;
    }

    public static double ComputeUsedDays(IEnumerable<TimeEntry> allEntries)
        => allEntries.Count(e => e.EntryType == EntryType.Vacation);

    public double ComputeRemainingDays(DateOnly today, IEnumerable<TimeEntry> allEntries)
        => ComputeAccruedDays(today) - ComputeUsedDays(allEntries);

    private DateOnly FirstResetOnOrAfter(DateOnly date)
    {
        var candidate = new DateOnly(date.Year, _config.VacationResetMonth, _config.VacationResetDay);
        return candidate >= date
            ? candidate
            : new DateOnly(date.Year + 1, _config.VacationResetMonth, _config.VacationResetDay);
    }
}
