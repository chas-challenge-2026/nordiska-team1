using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Nordiska.Modules.Banking.Application;

namespace Nordiska.Modules.Banking.Infrastructure.External.Riksbank;

/// <summary>
/// Typed HttpClient for the Riksbank SWEA API. The HttpClient is created and pooled by IHttpClientFactory,
/// so this class is registered as transient and must not be held on to by a singleton.
/// </summary>
public sealed class RiksbankClient : IRiksbankClient
{
    private readonly HttpClient _httpClient;
    private readonly RiksbankOptions _options;

    public RiksbankClient(HttpClient httpClient, IOptions<RiksbankOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<PolicyRateObservation> GetLatestPolicyRateAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"Observations/Latest/{_options.SeriesId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var observation = await response.Content.ReadFromJsonAsync<SweaObservation>(cancellationToken)
            ?? throw new HttpRequestException($"Riksbank returned an empty observation for {_options.SeriesId}.");

        return new PolicyRateObservation(observation.Date, observation.Value);
    }

    // Shape of GET Observations/Latest/{seriesId}, e.g. { "date": "2026-09-18", "value": 1.75 }
    private sealed record SweaObservation(DateOnly Date, decimal Value);
}
