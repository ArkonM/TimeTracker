namespace TimeTracker.Models;

public class AppData
{
    public List<TimeEntry> Entries { get; set; } = new();
    public AppSettings Settings { get; set; } = new();
    public ActiveSession? ActiveSession { get; set; }
}
