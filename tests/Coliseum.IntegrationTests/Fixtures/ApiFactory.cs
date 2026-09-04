using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Coliseum.Api;
using Coliseum.Api.Auth;
using Coliseum.Contracts.Auth;
using Coliseum.Contracts.Players;
using Coliseum.Worker;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Coliseum.IntegrationTests.Fixtures;

/// <summary>
/// Hosts the real API in-process against the test Redis under its own key prefix, with a known signing key and
/// API key. <see cref="WorkerFactory"/> does the same for the worker so end-to-end tests can run both.
/// </summary>
public sealed class ApiFactory(string redisConnectionString, string prefix) : WebApplicationFactory<ApiAssemblyMarker>
{
    public const string SigningKey = "integration-test-signing-key-0123456789abcdef";
    public const string ApiKey = "test-service-key";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Redis:ConnectionString"] = redisConnectionString,
            ["Redis:KeyPrefix"] = prefix,
            ["Auth:SigningKey"] = SigningKey,
            ["Auth:ApiKeys:0"] = ApiKey,
            ["RateLimit:PermitLimit"] = "10000",
        }));
    }

    /// <summary>Client authenticated with a service token obtained through the real API key exchange.</summary>
    public async Task<HttpClient> ServiceClientAsync(CancellationToken cancellationToken)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyExchange.HeaderName, ApiKey);
        var response = await client.PostAsync(new Uri("/auth/token", UriKind.Relative), content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<TokenResponse>(Json, cancellationToken))!;
        client.DefaultRequestHeaders.Remove(ApiKeyExchange.HeaderName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    /// <summary>Creates a player through the API and returns a client authenticated as that player.</summary>
    public async Task<(CreatePlayerResponse Player, HttpClient Client)> PlayerClientAsync(HttpClient service, string name, CancellationToken cancellationToken, int attack = 70, int defense = 30, int hitPoints = 100, long gold = 500, long silver = 120)
    {
        var response = await service.PostAsJsonAsync(new Uri("/players", UriKind.Relative), new CreatePlayerRequest(name, "test", gold, silver, attack, defense, hitPoints), Json, cancellationToken);
        response.EnsureSuccessStatusCode();
        var created = (await response.Content.ReadFromJsonAsync<CreatePlayerResponse>(Json, cancellationToken))!;

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", created.AccessToken);
        return (created, client);
    }
}

public sealed class WorkerFactory(string redisConnectionString, string prefix) : WebApplicationFactory<WorkerAssemblyMarker>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Redis:ConnectionString"] = redisConnectionString,
            ["Redis:KeyPrefix"] = prefix,
            ["Worker:PollInterval"] = "00:00:00.050",
            ["Worker:ClaimInterval"] = "00:00:01",
            ["Worker:ClaimMinIdle"] = "00:00:02",
            ["Worker:MaxConcurrency"] = "4",
        }));
    }
}
