using System.Text.Json.Serialization;

namespace TimeTracker.Models;

public class AppSettings
{
    public double WeeklyHours { get; set; } = 38.5;
    public int DefaultBreakMinutes { get; set; } = 30;

    // First day at the firm; vacation accrual and the yearly grant are based on this date.
    // Leave null to disable vacation tracking.
    public DateOnly? EmploymentStartDate { get; set; }
    public double VacationDaysPerYear { get; set; } = 25.0;

    [JsonIgnore]
    public TimeSpan DailyTarget => TimeSpan.FromHours(WeeklyHours / 5.0);
}
