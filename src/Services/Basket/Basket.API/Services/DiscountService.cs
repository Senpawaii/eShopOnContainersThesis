using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using Microsoft.Extensions.Options;
using System.Net.Http;
using YamlDotNet.Serialization;

namespace Microsoft.eShopOnContainers.Services.Basket.API.Services;
public class DiscountService : IDiscountService {
    private readonly IOptions<BasketSettings> _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<DiscountService> _logger;

    private readonly string _remoteServiceBaseUrl;

    public DiscountService(HttpClient httpClient, ILogger<DiscountService> logger, IOptions<BasketSettings> settings) {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings;

        _remoteServiceBaseUrl = settings.Value.DiscountUrl;
    }

    public async Task<decimal> GetDiscountValueAsync(string itemName, string brandName, string typeName) {
        string uri = $"{_remoteServiceBaseUrl}discounts?itemNames={itemName}&itemBrands={brandName}&itemTypes={typeName}";
        HttpResponseMessage response = await _httpClient.GetAsync(uri);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();

        // Extract Discount object from response: List of Discounts
        var deserializer = new DeserializerBuilder().Build();
        var yamlObject = deserializer.Deserialize(new StringReader(responseString));
        var discounts = (List<object>)yamlObject;
        
        var discount = discounts.FirstOrDefault();
        if (discount == null) {
            return 0;
        }
        var discountValue = Decimal.Parse(((Dictionary<object, object>)discount)["discountValue"].ToString());

        return discountValue;
    }
}
