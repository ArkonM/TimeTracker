namespace TimeTracker.Models;

public class VacationConfig
{
    public DateOnly EntryDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int VacationResetMonth { get; set; } = 1;
    public int VacationResetDay { get; set; } = 1;
    // Set to 0 to disable vacation tracking
    public double VacationDaysPerYear { get; set; } = 0.0;
}
