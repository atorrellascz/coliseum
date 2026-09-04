using System.Net.Http.Json;
using System.Text.Json;
using Coliseum.Contracts.Auth;
using Coliseum.Mcp.Options;
using Microsoft.Extensions.Options;

namespace Coliseum.Mcp;

/// <summary>
/// Process-wide cache of the service token obtained from the Coliseum API with the MCP server's API key.
/// Singleton on purpose: typed HttpClients are transient, so the cache cannot live in them.
/// Refreshes one minute before expiry; concurrent callers share a single refresh.
/// </summary>
public sealed class ServiceTokenProvider(IHttpClientFactory httpClientFactory, IOptions<McpOptions> options, TimeProvider timeProvider) : IDisposable
{
    public const string HttpClientName = "coliseum-api";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private TokenResponse? _token;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (IsFresh(_token))
        {
            return _token.AccessToken;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (IsFresh(_token))
            {
                return _token.AccessToken;
            }

            using var http = httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/token");
            request.Headers.Add("X-Api-Key", options.Value.ApiKey);
            using var response = await http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            _token = await response.Content.ReadFromJsonAsync<TokenResponse>(Json, cancellationToken)
                ?? throw new InvalidOperationException("Empty token response from the Coliseum API.");
            return _token.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Dispose() => _refreshLock.Dispose();

    private bool IsFresh([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] TokenResponse? token) =>
        token is not null && token.ExpiresAt > timeProvider.GetUtcNow().AddMinutes(1);
}
