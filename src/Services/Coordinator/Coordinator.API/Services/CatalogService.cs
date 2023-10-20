using IdentityModel;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using YamlDotNet.Serialization;

namespace Microsoft.eShopOnContainers.Services.Coordinator.API.Services;
public class CatalogService : ICatalogService {
    private readonly IOptions<CoordinatorSettings> _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CatalogService> _logger;

    private readonly string _remoteServiceBaseUrl;

    public CatalogService(HttpClient httpClient, ILogger<CatalogService> logger, IOptions<CoordinatorSettings> settings) {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings;

        _remoteServiceBaseUrl = settings.Value.CatalogUrl;
    }

    public async Task IssueCommit(string maxTS, string clientID) {
        string uri = $"{_settings.Value.CatalogUrl}commit?timestamp={maxTS}&clientID={clientID}";

        await _httpClient.GetAsync(uri);
    }

    public async Task<long> GetProposal(string clientID) {
        string uri = $"{_settings.Value.CatalogUrl}proposeTS?clientID={clientID}";
        HttpResponseMessage response = await _httpClient.GetAsync(uri);
        // response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();
        
        // Return the Timestamp Ticks received from the Catalog Service
        return long.Parse(responseString);
    }
}
