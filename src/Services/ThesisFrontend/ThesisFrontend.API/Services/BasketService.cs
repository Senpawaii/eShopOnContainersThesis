using System.Net.Http;
using YamlDotNet.Serialization;

namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Services;
public class BasketService : IBasketService {
    private IOptions<ThesisFrontendSettings> _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<BasketService> _logger;

    private readonly string _remoteBasketServiceBaseUrl;

    public BasketService(HttpClient httpClient, ILogger<BasketService> logger, IOptions<ThesisFrontendSettings> settings) { 
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings;

        _remoteBasketServiceBaseUrl = settings.Value.BasketUrl;
    }

    public async Task<string> GetBasketAsync(string basketId) {
        var uri = $"{_remoteBasketServiceBaseUrl}{basketId}";

        var response = await _httpClient.GetAsync(uri);
        if (response.StatusCode != HttpStatusCode.OK) {
            return null;
        }
        else {
            var responseString = await response.Content.ReadAsStringAsync();
            return responseString;
        }
    }
}
