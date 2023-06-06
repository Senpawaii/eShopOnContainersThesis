using Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;
using System.Net.Http;

namespace Microsoft.eShopOnContainers.Services.Discount.API.Services;

public class CoordinatorService : ICoordinatorService {
    private readonly IOptions<DiscountSettings> _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CoordinatorService> _logger;
    private readonly IScopedMetadata _metadata;
    private readonly ITokensContextSingleton _remainingTokens;


    public CoordinatorService(IOptions<DiscountSettings> settings, HttpClient httpClient, ILogger<CoordinatorService> logger, 
        IScopedMetadata scopedMetadata, ITokensContextSingleton remainingTokens) {
        _settings = settings;
        _httpClient = httpClient;
        _logger = logger;
        _metadata = scopedMetadata;
        _remainingTokens = remainingTokens;
    }

    public async Task SendTokens() {
        int tokensToSend = _remainingTokens.GetRemainingTokens(_metadata.ClientID);
        // Log the read/write operation
        if(_metadata.ReadOnly) {
            // _logger.LogInformation($"Func:<{_metadata.ClientID}> - Service:<DiscountService> - Tokens:<{tokensToSend}> - Read Only");
        }
        else {
            // _logger.LogInformation($"Func:<{_metadata.ClientID}> - Service:<DiscountService> - Tokens:<{tokensToSend}> - Write");
        }
        string uri = $"{_settings.Value.CoordinatorUrl}tokens?tokens={tokensToSend}&clientID={_metadata.ClientID}&serviceName=DiscountService&readOnly={_metadata.ReadOnly}";

        HttpResponseMessage response = await _httpClient.GetAsync(uri);

        var responseString = await response.Content.ReadAsStringAsync();
    }
}
