using System.Text;

namespace QuotaPet.Core;

public sealed class CodexSessionMonitor : IDisposable
{
    private sealed class TailState
    {
        public long Offset;
        public string Carry = string.Empty;
    }

    private readonly string _sessionsRoot;
    private readonly TimeSpan _debounce;
    private readonly object _gate = new();
    private readonly Dictionary<string, TailState> _tails = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _watcher;
    private Timer? _timer;
    private string? _pendingPath;
    private bool _disposed;

    public CodexSessionMonitor(string sessionsRoot, TimeSpan? debounce = null)
    {
        _sessionsRoot = sessionsRoot;
        _debounce = debounce ?? TimeSpan.FromMilliseconds(120);
    }

    public event EventHandler<UsageSnapshot>? UsageUpdated;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_watcher is not null || !Directory.Exists(_sessionsRoot)) return;

        foreach (var path in Directory.EnumerateFiles(_sessionsRoot, "*.jsonl", SearchOption.AllDirectories))
        {
            try { _tails[path] = new TailState { Offset = new FileInfo(path).Length }; }
            catch (IOException) { }
        }

        _watcher = new FileSystemWatcher(_sessionsRoot, "*.jsonl")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Renamed += OnFileRenamed;
        _watcher.EnableRaisingEvents = true;

        var latest = CodexUsageReader.ReadLatest(_sessionsRoot);
        if (latest is not null) UsageUpdated?.Invoke(this, latest);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e) => Schedule(e.FullPath);
    private void OnFileRenamed(object sender, RenamedEventArgs e) => Schedule(e.FullPath);

    private void Schedule(string path)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _pendingPath = path;
            _timer ??= new Timer(ProcessPending, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
    }

    private void ProcessPending(object? state)
    {
        string? path;
        lock (_gate)
        {
            path = _pendingPath;
            _pendingPath = null;
        }
        if (path is null) return;

        UsageSnapshot? latest = null;
        lock (_gate)
        {
            if (_disposed || !File.Exists(path)) return;
            if (!_tails.TryGetValue(path, out var tail))
            {
                tail = new TailState();
                _tails[path] = tail;
            }

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length < tail.Offset)
                {
                    tail.Offset = 0;
                    tail.Carry = string.Empty;
                }
                stream.Seek(tail.Offset, SeekOrigin.Begin);
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                tail.Offset = stream.Length;
                if (memory.Length == 0) return;

                var text = tail.Carry + Encoding.UTF8.GetString(memory.ToArray());
                var complete = text.EndsWith('\n');
                var lines = text.Split('\n');
                tail.Carry = complete ? string.Empty : lines[^1];
                var count = complete ? lines.Length : lines.Length - 1;
                for (var i = 0; i < count; i++)
                {
                    var line = lines[i].TrimEnd('\r');
                    if (CodexEventParser.TryParseTokenCount(line, out var parsed)) latest = parsed;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        if (latest is not null) UsageUpdated?.Invoke(this, latest);
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
                _watcher.Changed -= OnFileChanged;
                _watcher.Created -= OnFileChanged;
                _watcher.Renamed -= OnFileRenamed;
                _watcher.Dispose();
                _watcher = null;
            }
            _timer?.Dispose();
            _timer = null;
        }
    }
}
