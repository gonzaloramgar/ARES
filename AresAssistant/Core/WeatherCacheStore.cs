using Newtonsoft.Json;

namespace AresAssistant.Core;

public sealed class WeatherCacheEntry
{
    public string Key { get; set; } = "";
    public DateTime SavedAtUtc { get; set; }
    public string PayloadJson { get; set; } = "";
}

public sealed class WeatherCacheStore
{
    private readonly string _path;
    private readonly object _sync = new();
    private List<WeatherCacheEntry> _entries = new();

    public WeatherCacheStore(string path)
    {
        _path = path;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        Load();
    }

    public void Save(string key, string payloadJson)
    {
        lock (_sync)
        {
            var idx = _entries.FindIndex(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            var entry = new WeatherCacheEntry
            {
                Key = key,
                SavedAtUtc = DateTime.UtcNow,
                PayloadJson = payloadJson
            };

            if (idx >= 0) _entries[idx] = entry;
            else _entries.Add(entry);

            // Keep cache bounded.
            _entries = _entries
                .OrderByDescending(e => e.SavedAtUtc)
                .Take(40)
                .ToList();

            Persist();
        }
    }

    public WeatherCacheEntry? TryGet(string key, TimeSpan maxAge)
    {
        lock (_sync)
        {
            var hit = _entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (hit == null)
                return null;

            if ((DateTime.UtcNow - hit.SavedAtUtc) > maxAge)
                return null;

            return hit;
        }
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            Persist();
            return;
        }

        try
        {
            var raw = File.ReadAllText(_path);
            _entries = JsonConvert.DeserializeObject<List<WeatherCacheEntry>>(raw) ?? new List<WeatherCacheEntry>();
        }
        catch
        {
            _entries = new List<WeatherCacheEntry>();
            Persist();
        }
    }

    private void Persist()
    {
        var json = JsonConvert.SerializeObject(_entries, Formatting.Indented);
        File.WriteAllText(_path, json);
    }
}
