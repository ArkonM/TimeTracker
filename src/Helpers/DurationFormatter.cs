namespace TimeTracker.Helpers;

public static class DurationFormatter
{
    public static string Format(TimeSpan duration)
    {
        var d = duration.Duration();
        return $"{(int)d.TotalHours}:{d.Minutes:D2}";
    }

    public static string FormatSigned(TimeSpan duration)
    {
        if (duration == TimeSpan.Zero) return "0:00";
        var sign = duration < TimeSpan.Zero ? "-" : "+";
        var d = duration.Duration();
        return $"{sign}{(int)d.TotalHours}:{d.Minutes:D2}";
    }

    public static string FormatTime(TimeSpan time)
        => $"{(int)time.TotalHours:D2}:{time.Minutes:D2}";

    public static string FormatElapsed(TimeSpan elapsed)
    {
        var d = elapsed.Duration();
        return $"{(int)d.TotalHours:D2}:{d.Minutes:D2}:{d.Seconds:D2}";
    }
}
