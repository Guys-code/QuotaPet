using System.Net;
using System.Text;
using QuotaPet.Core;

namespace QuotaPet.Core.Tests;

public sealed class DeepSeekBalanceClientTests
{
    [Fact]
    public async Task GetBalanceAsync_ParsesOfficialResponse()
    {
        var handler = new StubHandler("""
        {
          "is_available": true,
          "balance_infos": [
            { "currency": "CNY", "total_balance": "18.25", "granted_balance": "3.25", "topped_up_balance": "15.00" }
          ]
        }
        """);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.deepseek.com") };
        var client = new DeepSeekBalanceClient(http);

        var result = await client.GetBalanceAsync("sk-test");

        Assert.True(result.IsAvailable);
        var balance = Assert.Single(result.Balances);
        Assert.Equal("CNY", balance.Currency);
        Assert.Equal(18.25m, balance.TotalBalance);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("sk-test", handler.AuthorizationParameter);
    }

    [Fact]
    public async Task GetBalanceAsync_ThrowsReadableErrorForUnauthorizedKey()
    {
        var handler = new StubHandler("{}", HttpStatusCode.Unauthorized);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.deepseek.com") };
        var client = new DeepSeekBalanceClient(http);

        var error = await Assert.ThrowsAsync<DeepSeekApiException>(() => client.GetBalanceAsync("bad-key"));

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
    }

    private sealed class StubHandler(string response, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            });
        }
    }
}
