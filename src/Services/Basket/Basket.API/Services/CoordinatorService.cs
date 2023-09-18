using Microsoft.eShopOnContainers.Services.Basket.API;
using Microsoft.eShopOnContainers.Services.Basket.API.DependencyServices;
using System.Net.Http;

namespace Microsoft.eShopOnContainers.Services.Basket.API.Services;

public class CoordinatorService : ICoordinatorService {
    private readonly IOptions<BasketSettings> _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CoordinatorService> _logger;
    private readonly IScopedMetadata _metadata;
    private readonly ITokensContextSingleton _remainingTokens;


    public CoordinatorService(IOptions<BasketSettings> settings, HttpClient httpClient, ILogger<CoordinatorService> logger, 
        IScopedMetadata scopedMetadata, ITokensContextSingleton remainingTokens) {
        _settings = settings;
        _httpClient = httpClient;
        _logger = logger;
        _metadata = scopedMetadata;
        _remainingTokens = remainingTokens;
    }

    public async Task SendTokens() {
        int tokensToSend = _remainingTokens.GetRemainingTokens(_metadata.ClientID.Value);
        string uri = $"{_settings.Value.CoordinatorUrl}tokens?tokens={tokensToSend}&clientID={_metadata.ClientID.Value}&serviceName=BasketService&readOnly={_metadata.ReadOnly.Value}";
        
        HttpResponseMessage response = await _httpClient.GetAsync(uri);

        var responseString = await response.Content.ReadAsStringAsync();
    }

    public async Task QueryConfirmation() {
        string uri = $"{_settings.Value.CoordinatorUrl}confirmation?clientID={_metadata.ClientID.Value}&serviceName=BasketService";

        HttpResponseMessage response = await _httpClient.GetAsync(uri);

        var responseString = await response.Content.ReadAsStringAsync();

        // Return the response to the caller
    }

}
