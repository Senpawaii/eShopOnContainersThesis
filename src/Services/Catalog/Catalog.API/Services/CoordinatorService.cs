using Catalog.API.DependencyServices;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.eShopOnContainers.Services.Catalog.API.DependencyServices;
using System.Net.Http;

namespace Microsoft.eShopOnContainers.Services.Catalog.API.Services;

public class CoordinatorService : ICoordinatorService {
    private readonly IOptions<CatalogSettings> _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CoordinatorService> _logger;
    private readonly IScopedMetadata _metadata;

    public CoordinatorService(IOptions<CatalogSettings> settings, HttpClient httpClient, ILogger<CoordinatorService> logger, IScopedMetadata scopedMetadata) {
        _settings = settings;
        _httpClient = httpClient;
        _logger = logger;
        _metadata = scopedMetadata;
    }

    public async Task SendTokens() {
        string uri = $"{_settings.Value.CoordinatorUrl}tokens?tokens={_metadata.Tokens}&clientID={_metadata.ClientID}&serviceName=CatalogService&readOnly={_metadata.ReadOnly}";

        HttpResponseMessage response = await _httpClient.GetAsync(uri);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
    }

}
