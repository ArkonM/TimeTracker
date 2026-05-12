namespace TimeTracker.Services;

public record HolidayEntry(DateOnly Date, string Name);

public class HolidayService
{
    private readonly Dictionary<int, List<HolidayEntry>> _cache = new();

    private List<HolidayEntry> GetHolidays(int year)
    {
        if (_cache.TryGetValue(year, out var cached)) return cached;

        var easter = CalcEaster(year);
        var list = new List<HolidayEntry>
        {
            new(new DateOnly(year, 1,  1),  "Neujahr"),
            new(new DateOnly(year, 1,  6),  "Heilige Drei Könige"),
            new(DateOnly.FromDateTime(easter.AddDays(1)),  "Ostermontag"),
            new(new DateOnly(year, 5,  1),  "Staatsfeiertag"),
            new(DateOnly.FromDateTime(easter.AddDays(39)), "Christi Himmelfahrt"),
            new(DateOnly.FromDateTime(easter.AddDays(50)), "Pfingstmontag"),
            new(DateOnly.FromDateTime(easter.AddDays(60)), "Fronleichnam"),
            new(new DateOnly(year, 8,  15), "Mariä Himmelfahrt"),
            new(new DateOnly(year, 10, 26), "Nationalfeiertag"),
            new(new DateOnly(year, 11, 1),  "Allerheiligen"),
            new(new DateOnly(year, 12, 8),  "Mariä Empfängnis"),
            new(new DateOnly(year, 12, 25), "Weihnachten"),
            new(new DateOnly(year, 12, 26), "Stefanitag"),
        };

        _cache[year] = list;
        return list;
    }

    public bool IsHoliday(DateOnly date)
        => GetHolidays(date.Year).Any(h => h.Date == date);

    public string? GetHolidayName(DateOnly date)
        => GetHolidays(date.Year).FirstOrDefault(h => h.Date == date)?.Name;

    // Anonymous Gregorian algorithm for Easter Sunday
    private static DateTime CalcEaster(int year)
    {
        int a = year % 19, b = year / 100, c = year % 100;
        int d = b / 4, e = b % 4, f = (b + 8) / 25;
        int g = (b - f + 1) / 3, h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateTime(year, month, day);
    }
}
