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
    private readonly string _remoteDiscountServiceBaseUrl;

    public CatalogService(HttpClient httpClient, ILogger<CatalogService> logger, IOptions<BasketSettings> settings) {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings;

        _remoteCatalogServiceBaseUrl = settings.Value.CatalogUrl;
        _remoteDiscountServiceBaseUrl = settings.Value.DiscountUrl;
    }

    public async Task<decimal> GetCatalogItemPriceAsync(string productName, string productBrand, string productType) {
        // Get Item

        //HttpResponseMessage response = await _httpClient.GetAsync(uri);
        //response.EnsureSuccessStatusCode();

        //var responseString = await response.Content.ReadAsStringAsync();

        //// Extract Item Name, BrandId, TypeId from Catalog Item returned
        //var deserializer = new DeserializerBuilder().Build();
        //var yamlObject = deserializer.Deserialize(new StringReader(responseString));
        //var item = (Dictionary<object, object>)yamlObject;
        //var itemName = item["name"];
        //var brandId = item["catalogBrandId"];
        //var typeId = item["catalogTypeId"];


        //// Get Brands
        //uri = $"{_remoteCatalogServiceBaseUrl}catalogbrands";
        //// Get Brand name from Brand with Id = BrandId
        //response = await _httpClient.GetAsync(uri);
        //response.EnsureSuccessStatusCode();
        //responseString = await response.Content.ReadAsStringAsync();
        //deserializer = new DeserializerBuilder().Build();
        //yamlObject = deserializer.Deserialize(new StringReader(responseString));
        //var brands = (List<object>)yamlObject;
        //var brand = brands.Where(b => ((Dictionary<object, object>)b)["id"].ToString() == brandId.ToString()).FirstOrDefault();
        //var brandName = ((Dictionary<object, object>)brand)["brand"];


        //// Get Types
        //uri = $"{_remoteCatalogServiceBaseUrl}catalogtypes";
        //// Get Type name from Type with Id = TypeId
        //response = await _httpClient.GetAsync(uri);
        //response.EnsureSuccessStatusCode();
        //responseString = await response.Content.ReadAsStringAsync();
        //deserializer = new DeserializerBuilder().Build();
        //yamlObject = deserializer.Deserialize(new StringReader(responseString));
        //var types = (List<object>)yamlObject;
        //var type = types.Where(t => ((Dictionary<object, object>)t)["id"].ToString() == typeId.ToString()).FirstOrDefault();
        //var typeName = ((Dictionary<object, object>)type)["type"];

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
