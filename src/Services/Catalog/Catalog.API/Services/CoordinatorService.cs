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
    private readonly ITokensContextSingleton _remainingTokens;


    public CoordinatorService(IOptions<CatalogSettings> settings, HttpClient httpClient, ILogger<CoordinatorService> logger, 
        IScopedMetadata scopedMetadata, ITokensContextSingleton remainingTokens) {
        _settings = settings;
        _httpClient = httpClient;
        _logger = logger;
        _metadata = scopedMetadata;
        _remainingTokens = remainingTokens;
    }

    public async Task SendTokens() {
        //int tokensToSend = _remainingTokens.GetRemainingTokens(_metadata.ClientID);
        int tokensToSend = _metadata.Tokens;
        // _logger.LogInformation($"ClientID {_metadata.ClientID}: Send {tokensToSend} tokens to coordinator. Readonly={_metadata.ReadOnly}");
        string uri = $"{_settings.Value.CoordinatorUrl}tokens?tokens={tokensToSend}&clientID={_metadata.ClientID}&serviceName=CatalogService&readOnly={_metadata.ReadOnly}";

        HttpResponseMessage response = await _httpClient.GetAsync(uri);

        var responseString = await response.Content.ReadAsStringAsync();
    }

    public async Task Ping() {
        string uri = $"{_settings.Value.CoordinatorUrl}ping?clientID={_metadata.ClientID}";

        HttpResponseMessage response = await _httpClient.GetAsync(uri);

        _logger.LogInformation($"ClientID: {_metadata.ClientID}, Pinged Coordinator. Response: {response.StatusCode}");
    }

}
