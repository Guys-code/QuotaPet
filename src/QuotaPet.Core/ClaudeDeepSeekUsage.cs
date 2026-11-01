using System.Text;
using System.Text.Json;

namespace QuotaPet.Core;

public sealed record DeepSeekTokenCounts(
    long InputTokens = 0,
    long OutputTokens = 0,
    long CacheCreationInputTokens = 0,
    long CacheReadInputTokens = 0)
{
    public long TotalTokens => InputTokens + OutputTokens + CacheCreationInputTokens + CacheReadInputTokens;

    public static DeepSeekTokenCounts operator +(DeepSeekTokenCounts left, DeepSeekTokenCounts right) => new(
        left.InputTokens + right.InputTokens,
        left.OutputTokens + right.OutputTokens,
        left.CacheCreationInputTokens + right.CacheCreationInputTokens,
        left.CacheReadInputTokens + right.CacheReadInputTokens);
}

public sealed record ClaudeDeepSeekUsageRecord(
    string MessageId,
    string Model,
    DateTimeOffset ObservedAt,
    DeepSeekTokenCounts Tokens);

public sealed record ClaudeDeepSeekUsageSnapshot(
    DateTimeOffset ObservedAt,
    int RequestCount,
    DeepSeekTokenCounts Total,
    DeepSeekTokenCounts Today,
    DeepSeekTokenCounts LastRequest)
{
    public static ClaudeDeepSeekUsageSnapshot Empty { get; } = new(
        DateTimeOffset.MinValue, 0, new(), new(), new());
}

public static class ClaudeDeepSeekEventParser
{
    public static bool TryParse(string line, out ClaudeDeepSeekUsageRecord record)
    {
        record = default!;
        if (string.IsNullOrWhiteSpace(line)) return false;
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object) return false;
            if (!TryString(message, "model", out var model) ||
                !model.StartsWith("deepseek", StringComparison.OrdinalIgnoreCase)) return false;
            if (!TryString(message, "id", out var id) || string.IsNullOrWhiteSpace(id)) return false;
            if (!message.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object) return false;

            var timestamp = DateTimeOffset.MinValue;
            if (TryString(root, "timestamp", out var timestampText))
                DateTimeOffset.TryParse(timestampText, out timestamp);
            var tokens = new DeepSeekTokenCounts(
                Number(usage, "input_tokens"),
                Number(usage, "output_tokens"),
                Number(usage, "cache_creation_input_tokens"),
                Number(usage, "cache_read_input_tokens"));
            record = new ClaudeDeepSeekUsageRecord(id, model, timestamp, tokens);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryString(JsonElement element, string name, out string value)
    {
        value = string.Empty;
        return element.TryGetProperty(name, out var property) &&
               property.ValueKind == JsonValueKind.String &&
               (value = property.GetString() ?? string.Empty).Length > 0;
    }

    private static long Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property) && property.TryGetInt64(out var value) ? value : 0;
}

public static class ClaudeDeepSeekUsageReader
{
    public static ClaudeDeepSeekUsageSnapshot Read(string projectsRoot, DateOnly? localDate = null) =>
        Aggregate(ReadRecords(projectsRoot), localDate ?? DateOnly.FromDateTime(DateTime.Now));

    internal static Dictionary<string, ClaudeDeepSeekUsageRecord> ReadRecords(string projectsRoot)
    {
        var records = new Dictionary<string, ClaudeDeepSeekUsageRecord>(StringComparer.Ordinal);
        if (!Directory.Exists(projectsRoot)) return records;
        try
        {
            foreach (var path in Directory.EnumerateFiles(projectsRoot, "*.jsonl", SearchOption.AllDirectories))
                ReadFile(path, records);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return records;
    }

    internal static void ReadFile(string path, Dictionary<string, ClaudeDeepSeekUsageRecord> records)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, true, 4096);
            while (reader.ReadLine() is { } line)
                if (ClaudeDeepSeekEventParser.TryParse(line, out var record)) Upsert(records, record);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    internal static void Upsert(Dictionary<string, ClaudeDeepSeekUsageRecord> records, ClaudeDeepSeekUsageRecord record)
    {
        if (!records.TryGetValue(record.MessageId, out var existing) ||
            record.Tokens.TotalTokens > existing.Tokens.TotalTokens ||
            record.Tokens.TotalTokens == existing.Tokens.TotalTokens && record.ObservedAt > existing.ObservedAt)
            records[record.MessageId] = record;
    }

    internal static ClaudeDeepSeekUsageSnapshot Aggregate(
        Dictionary<string, ClaudeDeepSeekUsageRecord> records,
        DateOnly localDate)
    {
        var total = new DeepSeekTokenCounts();
        var today = new DeepSeekTokenCounts();
        ClaudeDeepSeekUsageRecord? latest = null;
        foreach (var record in records.Values)
        {
            total += record.Tokens;
            if (record.ObservedAt != DateTimeOffset.MinValue &&
                DateOnly.FromDateTime(record.ObservedAt.LocalDateTime) == localDate)
                today += record.Tokens;
            if (latest is null || record.ObservedAt > latest.ObservedAt) latest = record;
        }
        return new ClaudeDeepSeekUsageSnapshot(
            latest?.ObservedAt ?? DateTimeOffset.MinValue,
            records.Count,
            total,
            today,
            latest?.Tokens ?? new DeepSeekTokenCounts());
    }
}

public sealed class ClaudeDeepSeekUsageMonitor : IDisposable
{
    private sealed class TailState
    {
        public long Offset;
        public string Carry = string.Empty;
    }

    private readonly string _projectsRoot;
    private readonly TimeSpan _debounce;
    private readonly object _gate = new();
    private readonly Dictionary<string, TailState> _tails = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pending = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, ClaudeDeepSeekUsageRecord> _records = new(StringComparer.Ordinal);
    private FileSystemWatcher? _watcher;
    private Timer? _timer;
    private bool _disposed;

    public ClaudeDeepSeekUsageMonitor(string projectsRoot, TimeSpan? debounce = null)
    {
        _projectsRoot = projectsRoot;
        _debounce = debounce ?? TimeSpan.FromMilliseconds(250);
    }

    public event EventHandler<ClaudeDeepSeekUsageSnapshot>? UsageUpdated;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_watcher is not null || !Directory.Exists(_projectsRoot)) return;
        _records = ClaudeDeepSeekUsageReader.ReadRecords(_projectsRoot);
        foreach (var path in Directory.EnumerateFiles(_projectsRoot, "*.jsonl", SearchOption.AllDirectories))
        {
            try { _tails[path] = new TailState { Offset = new FileInfo(path).Length }; }
            catch (IOException) { }
        }
        _watcher = new FileSystemWatcher(_projectsRoot, "*.jsonl")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Renamed += OnRenamed;
        _watcher.EnableRaisingEvents = true;
        UsageUpdated?.Invoke(this, ClaudeDeepSeekUsageReader.Aggregate(
            _records, DateOnly.FromDateTime(DateTime.Now)));
    }

    private void OnChanged(object sender, FileSystemEventArgs e) => Schedule(e.FullPath);
    private void OnRenamed(object sender, RenamedEventArgs e) => Schedule(e.FullPath);

    private void Schedule(string path)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _pending.Add(path);
            _timer ??= new Timer(ProcessPending, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
    }

    private void ProcessPending(object? state)
    {
        string[] paths;
        ClaudeDeepSeekUsageSnapshot? snapshot = null;
        lock (_gate)
        {
            if (_disposed) return;
            paths = _pending.ToArray();
            _pending.Clear();
            var changed = false;
            foreach (var path in paths)
                changed |= ReadTail(path);
            if (changed)
                snapshot = ClaudeDeepSeekUsageReader.Aggregate(_records, DateOnly.FromDateTime(DateTime.Now));
        }
        if (snapshot is not null) UsageUpdated?.Invoke(this, snapshot);
    }

    private bool ReadTail(string path)
    {
        if (!File.Exists(path)) return false;
        if (!_tails.TryGetValue(path, out var tail))
        {
            tail = new TailState();
            _tails[path] = tail;
        }
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length < tail.Offset)
            {
                _records = ClaudeDeepSeekUsageReader.ReadRecords(_projectsRoot);
                tail.Offset = stream.Length;
                tail.Carry = string.Empty;
                return true;
            }
            stream.Seek(tail.Offset, SeekOrigin.Begin);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            tail.Offset = stream.Length;
            if (memory.Length == 0) return false;
            var text = tail.Carry + Encoding.UTF8.GetString(memory.ToArray());
            var complete = text.EndsWith('\n');
            var lines = text.Split('\n');
            tail.Carry = complete ? string.Empty : lines[^1];
            var count = complete ? lines.Length : lines.Length - 1;
            var changed = false;
            for (var index = 0; index < count; index++)
            {
                if (!ClaudeDeepSeekEventParser.TryParse(lines[index].TrimEnd('\r'), out var record)) continue;
                var before = _records.GetValueOrDefault(record.MessageId);
                ClaudeDeepSeekUsageReader.Upsert(_records, record);
                changed |= before != _records.GetValueOrDefault(record.MessageId);
            }
            return changed;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            if (_watcher is not null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnChanged;
                _watcher.Created -= OnChanged;
                _watcher.Renamed -= OnRenamed;
                _watcher.Dispose();
            }
            _timer?.Dispose();
        }
    }
}
