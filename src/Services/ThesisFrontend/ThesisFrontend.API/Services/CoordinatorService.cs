using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.DependencyServices;
using System.Net.Http;

namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Services;

public class CoordinatorService : ICoordinatorService {
    private readonly IOptions<ThesisFrontendSettings> _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CoordinatorService> _logger;
    private readonly IScopedMetadata _metadata;
    private readonly ITokensContextSingleton _remainingTokens;

    public CoordinatorService(IOptions<ThesisFrontendSettings> settings, HttpClient httpClient, ILogger<CoordinatorService> logger, 
        IScopedMetadata scopedMetadata, ITokensContextSingleton remainingTokens) {
        _settings = settings;
        _httpClient = httpClient;
        _logger = logger;
        _metadata = scopedMetadata;
        _remainingTokens = remainingTokens;
    }

    public async Task SendTokens() {
        // _logger.LogInformation($"TF1.1 at {DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt", CultureInfo.InvariantCulture)} for clientID {_metadata.ClientID.Value}, with ReadOnly={_metadata.ReadOnly.Value}.");

        int tokensToSend = _remainingTokens.GetRemainingTokens(_metadata.ClientID.Value);
        string uri = $"{_settings.Value.CoordinatorUrl}tokens?tokens={tokensToSend}&clientID={_metadata.ClientID.Value}&serviceName=ThesisFrontendService&readOnly={_metadata.ReadOnly.Value}";
        
        HttpResponseMessage response = await _httpClient.GetAsync(uri);

        var responseString = await response.Content.ReadAsStringAsync();
        // _logger.LogInformation($"TF1.2 at {DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt", CultureInfo.InvariantCulture)} for functionality {_metadata.ClientID.Value}.");

    }

}
