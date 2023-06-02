using System.Net.Http;
using YamlDotNet.Serialization;
using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Model;


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

    public async Task<BasketData> GetBasketAsync(string basketId) {
        var uri = $"{_remoteBasketServiceBaseUrl}{basketId}";

        var response = await _httpClient.GetAsync(uri);
        if (response.StatusCode != HttpStatusCode.OK) {
            return null;
        }
        else {
            var responseString = await response.Content.ReadAsStringAsync();
            var deserializer = new DeserializerBuilder().Build();
            var basketData = deserializer.Deserialize<BasketData>(responseString);
            return basketData;
        }
    }

    public async Task<BasketData> UpdateBasketAsync(BasketData currentBasket) {
        var uri = $"{_remoteBasketServiceBaseUrl}";

        // Send the current basket to the basket service on HTTP POST Body
        var basketData = new StringContent(currentBasket.ToString(), System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(uri, basketData);

        if (response.StatusCode != HttpStatusCode.OK) {
            return null;
        }
        else {
            var responseString = await response.Content.ReadAsStringAsync();
            var deserializer = new DeserializerBuilder().Build();
            var basket = deserializer.Deserialize<BasketData>(responseString);
            return basket;
        }
    }
}
