namespace TimeTracker.Models;

public class AppData
{
    public AppSettings Settings { get; set; } = new();
    public ActiveSession? ActiveSession { get; set; }
}
