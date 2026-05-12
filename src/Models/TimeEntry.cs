namespace TimeTracker.Models;

public enum EntryType { Work, Vacation, Sick }

public class TimeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly Date { get; set; }
    public EntryType EntryType { get; set; } = EntryType.Work;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public TimeSpan BreakDuration { get; set; }
    public TimeSpan WorkDuration => EntryType == EntryType.Work ? EndTime - StartTime - BreakDuration : TimeSpan.Zero;
    public bool AutoStopped { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
