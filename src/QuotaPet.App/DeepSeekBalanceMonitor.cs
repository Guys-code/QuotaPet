using System.Net;
using QuotaPet.Core;

namespace QuotaPet.App;

internal sealed record DeepSeekViewState(
    bool IsConfigured,
    bool IsLoading,
    DeepSeekBalanceSnapshot? Snapshot,
    DeepSeekLedgerState? Ledger,
    string? Error)
{
    public static DeepSeekViewState NotConfigured { get; } = new(false, false, null, null, null);
}

internal sealed class DeepSeekBalanceMonitor : IDisposable
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("https://api.deepseek.com"), Timeout = TimeSpan.FromSeconds(15) };
    private readonly DeepSeekBalanceClient _client;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly System.Threading.Timer _timer;
    private DeepSeekLedgerState? _ledger;
    private bool _disposed;

    public DeepSeekBalanceMonitor(DeepSeekLedgerState? ledger)
    {
        _ledger = ledger;
        _client = new DeepSeekBalanceClient(_http);
        _timer = new System.Threading.Timer(_ => _ = RefreshAsync(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public event EventHandler<DeepSeekViewState>? Updated;

    public void Start()
    {
        _timer.Change(TimeSpan.Zero, TimeSpan.FromMinutes(5));
    }

    public async Task RefreshAsync()
    {
        if (_disposed || !await _refreshLock.WaitAsync(0).ConfigureAwait(false)) return;
        try
        {
            var key = DeepSeekCredentialStore.Read();
            if (string.IsNullOrWhiteSpace(key))
            {
                Updated?.Invoke(this, DeepSeekViewState.NotConfigured);
                return;
            }

            Updated?.Invoke(this, new DeepSeekViewState(true, true, null, _ledger, null));
            var snapshot = await _client.GetBalanceAsync(key).ConfigureAwait(false);
            _ledger = DeepSeekSpendTracker.Update(
                _ledger, DeepSeekSpendTracker.Fingerprint(key), snapshot.Balances, snapshot.ObservedAt);
            Updated?.Invoke(this, new DeepSeekViewState(true, false, snapshot, _ledger, null));
        }
        catch (DeepSeekApiException ex)
        {
            var error = ex.StatusCode == HttpStatusCode.Unauthorized ? "API Key 无效，请重新设置" : ex.Message;
            Updated?.Invoke(this, new DeepSeekViewState(true, false, null, _ledger, error));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            Updated?.Invoke(this, new DeepSeekViewState(true, false, null, _ledger, "暂时无法查询 DeepSeek 余额"));
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task<DeepSeekLedgerState> ImportOfficialCostsAsync(
        IReadOnlyList<DeepSeekOfficialCostEntry> entries)
    {
        await _refreshLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _ledger ??= new DeepSeekLedgerState(
                "official-import",
                new Dictionary<string, DeepSeekCurrencyLedger>(StringComparer.OrdinalIgnoreCase));
            _ledger = DeepSeekOfficialCostImporter.Merge(_ledger, entries);
            return _ledger;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();
        _http.Dispose();
        _refreshLock.Dispose();
    }
}
