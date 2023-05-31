using IdentityModel;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using YamlDotNet.Serialization;

namespace Microsoft.eShopOnContainers.Services.Basket.API.Services;
public class CatalogService : ICatalogService {
    private readonly IOptions<BasketSettings> _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CatalogService> _logger;

    private readonly string _remoteCatalogServiceBaseUrl;

    public CatalogService(HttpClient httpClient, ILogger<CatalogService> logger, IOptions<BasketSettings> settings) {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings;

        _remoteCatalogServiceBaseUrl = settings.Value.CatalogUrl;
    }

    public async Task<decimal> GetCatalogItemPriceAsync(string productName, string productBrand, string productType) {
        // Replace the "&" with "%26" to avoid the error: "The request URI is invalid because it contains invalid characters."
        productName = productName.Replace("&", "%26");

        // Get Item Price
        var uri = $"{_remoteCatalogServiceBaseUrl}items/name/{productName}/brand/{productBrand}/type/{productType}/price";

        var response = await _httpClient.GetAsync(uri);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        // Extract Item Price from Catalog Item returned

        var deserializer = new DeserializerBuilder().Build();
        var yamlObject = deserializer.Deserialize(new StringReader(responseString));
        var price = Decimal.Parse(yamlObject.ToString());
        
        // Return Item Price
        return decimal.Parse(price.ToString());
    }
}
