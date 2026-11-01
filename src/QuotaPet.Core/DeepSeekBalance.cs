using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuotaPet.Core;

public sealed record DeepSeekBalance(
    string Currency,
    decimal TotalBalance,
    decimal GrantedBalance,
    decimal ToppedUpBalance);

public sealed record DeepSeekBalanceSnapshot(
    DateTimeOffset ObservedAt,
    bool IsAvailable,
    IReadOnlyList<DeepSeekBalance> Balances);

public sealed class DeepSeekApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

public sealed class DeepSeekBalanceClient(HttpClient httpClient)
{
    public async Task<DeepSeekBalanceSnapshot> GetBalanceAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/user/balance");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new DeepSeekApiException(response.StatusCode, ErrorMessage(response.StatusCode));

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<BalanceResponse>(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false) ?? throw new JsonException("DeepSeek 返回了空响应。");

        var balances = (payload.BalanceInfos ?? []).Select(info => new DeepSeekBalance(
            info.Currency,
            ParseAmount(info.TotalBalance),
            ParseAmount(info.GrantedBalance),
            ParseAmount(info.ToppedUpBalance))).ToArray();
        return new DeepSeekBalanceSnapshot(DateTimeOffset.Now, payload.IsAvailable, balances);
    }

    private static decimal ParseAmount(string value) =>
        decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);

    private static string ErrorMessage(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => "API Key 无效或已失效。",
        HttpStatusCode.PaymentRequired => "DeepSeek 账户余额不足。",
        (HttpStatusCode)429 => "DeepSeek 请求过于频繁，请稍后重试。",
        _ => $"DeepSeek 接口返回错误 {(int)statusCode}。"
    };

    private sealed record BalanceResponse(
        [property: JsonPropertyName("is_available")] bool IsAvailable,
        [property: JsonPropertyName("balance_infos")] BalanceInfo[] BalanceInfos);

    private sealed record BalanceInfo(
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("total_balance")] string TotalBalance,
        [property: JsonPropertyName("granted_balance")] string GrantedBalance,
        [property: JsonPropertyName("topped_up_balance")] string ToppedUpBalance);
}
