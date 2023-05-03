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

    public async Task IssueCommit(string maxTS) {
        string uri = $"{_settings.Value.CatalogUrl}commit?timestamp={maxTS}";

        HttpResponseMessage response = await _httpClient.GetAsync(uri);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
    }

    
}
