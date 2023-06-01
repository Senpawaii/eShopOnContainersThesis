using Microsoft.eShopOnContainers.Services.Basket.API;
using Microsoft.eShopOnContainers.Services.Basket.API.DependencyServices;
using System.Net.Http;

namespace Microsoft.eShopOnContainers.Services.Basket.API.Services;

public class CoordinatorService : ICoordinatorService {
    private readonly IOptions<BasketSettings> _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CoordinatorService> _logger;
    private readonly IScopedMetadata _metadata;

    public CoordinatorService(IOptions<BasketSettings> settings, HttpClient httpClient, ILogger<CoordinatorService> logger, IScopedMetadata scopedMetadata) {
        _settings = settings;
        _httpClient = httpClient;
        _logger = logger;
        _metadata = scopedMetadata;
    }

    public async Task SendTokens() {
        string uri = $"{_settings.Value.CoordinatorUrl}tokens?tokens={_metadata.Tokens.Value}&funcID={_metadata.ClientID}&serviceName=DiscountService&readOnly={_metadata.ReadOnly}";
        
        HttpResponseMessage response = await _httpClient.GetAsync(uri);
        //response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
    }

}
