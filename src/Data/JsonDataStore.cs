using System.IO;
using System.Text.Json;
using TimeTracker.Helpers;
using TimeTracker.Models;

namespace TimeTracker.Data;

public class JsonDataStore
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TimeTracker");

    private static readonly string MetaPath = Path.Combine(Dir, "data.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new TimeSpanConverter(), new DateOnlyConverter() }
    };

    private static string EntriesPath(int month) =>
        Path.Combine(Dir, $"{month:D2}_Entries.json");

    public AppData LoadMeta()
    {
        if (!File.Exists(MetaPath))
            return new AppData();
        try
        {
            var json = File.ReadAllText(MetaPath);
            return JsonSerializer.Deserialize<AppData>(json, Options) ?? new AppData();
        }
        catch
        {
            return new AppData();
        }
    }

    public void SaveMeta(AppData data)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(MetaPath, JsonSerializer.Serialize(data, Options));
    }

    public List<TimeEntry> LoadEntries(int month)
    {
        var path = EntriesPath(month);
        if (!File.Exists(path))
            return new List<TimeEntry>();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<TimeEntry>>(json, Options) ?? new List<TimeEntry>();
        }
        catch
        {
            return new List<TimeEntry>();
        }
    }

    public void SaveEntries(int month, List<TimeEntry> entries)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(EntriesPath(month), JsonSerializer.Serialize(entries, Options));
    }

    public IEnumerable<int> GetMonthsWithEntries()
    {
        if (!Directory.Exists(Dir)) yield break;
        foreach (var file in Directory.GetFiles(Dir, "??_Entries.json"))
        {
            var stem = Path.GetFileNameWithoutExtension(file);
            if (int.TryParse(stem[..2], out var month) && month >= 1 && month <= 12)
                yield return month;
        }
    }
}
