using System.Text.Json.Serialization;

namespace TimeTracker.Models;

public class AppSettings
{
    public double WeeklyHours { get; set; } = 38.5;
    public double OvertimeThresholdHours { get; set; } = 8.0;
    public int DefaultBreakMinutes { get; set; } = 30;

    [JsonIgnore]
    public TimeSpan DailyTarget => TimeSpan.FromHours(WeeklyHours / 5.0);

    [JsonIgnore]
    public TimeSpan OvertimeThreshold => TimeSpan.FromHours(OvertimeThresholdHours);
}
