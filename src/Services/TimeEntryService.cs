using TimeTracker.Data;
using TimeTracker.Models;

namespace TimeTracker.Services;

public class TimeEntryService
{
    private readonly JsonDataStore _store = new();
    private AppData _meta;
    private readonly Dictionary<int, List<TimeEntry>> _monthCache = new();

    public TimeEntryService() => _meta = _store.LoadMeta();

    public AppSettings Settings => _meta.Settings;
    public ActiveSession? ActiveSession => _meta.ActiveSession;

    private List<TimeEntry> GetOrLoadMonth(int month)
    {
        if (!_monthCache.TryGetValue(month, out var list))
        {
            list = _store.LoadEntries(month);
            _monthCache[month] = list;
        }
        return list;
    }

    public IReadOnlyList<TimeEntry> GetEntriesForMonth(int year, int month)
        => GetOrLoadMonth(month)
            .Where(e => e.Date.Year == year && e.Date.Month == month)
            .OrderBy(e => e.Date).ThenBy(e => e.StartTime)
            .ToList();

    public void AddEntry(TimeEntry entry)
    {
        var list = GetOrLoadMonth(entry.Date.Month);
        list.Add(entry);
        _store.SaveEntries(entry.Date.Month, list);
    }

    public void UpdateEntry(TimeEntry entry)
    {
        var list = GetOrLoadMonth(entry.Date.Month);
        var idx = list.FindIndex(e => e.Id == entry.Id);
        if (idx >= 0) list[idx] = entry;
        _store.SaveEntries(entry.Date.Month, list);
    }

    public void DeleteEntry(Guid id, DateOnly date)
    {
        var list = GetOrLoadMonth(date.Month);
        list.RemoveAll(e => e.Id == id);
        _store.SaveEntries(date.Month, list);
    }

    public IEnumerable<(int Year, int Month)> GetMonthsWithEntries()
        => GetAllEntries().Select(e => (e.Date.Year, e.Date.Month)).Distinct();

    public IReadOnlyList<TimeEntry> GetAllEntries()
        => _store.GetMonthsWithEntries()
            .SelectMany(m => GetOrLoadMonth(m))
            .OrderBy(e => e.Date).ThenBy(e => e.StartTime)
            .ToList();

    public TimeEntry? GetEntryForDate(DateOnly date)
        => GetOrLoadMonth(date.Month)
            .FirstOrDefault(e => e.Date == date && e.EntryType == EntryType.Work);

    public void StartSession() { _meta.ActiveSession = new ActiveSession { StartedAt = DateTime.Now }; SaveMeta(); }
    public void ClearSession() { _meta.ActiveSession = null; SaveMeta(); }
    public void SaveSettings() => SaveMeta();

    private void SaveMeta() => _store.SaveMeta(_meta);
}
