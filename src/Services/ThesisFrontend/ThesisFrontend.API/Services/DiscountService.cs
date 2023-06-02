using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Text;

namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Services;
public class DiscountService : IDiscountService {
    private readonly HttpClient _httpClient;
    private readonly ILogger<DiscountService> _logger;

    private readonly string _remoteDiscountServiceBaseUrl;

    public DiscountService(HttpClient httpClient, ILogger<DiscountService> logger, IOptions<ThesisFrontendSettings> settings) { 
        _httpClient = httpClient;
        _logger = logger;
    
        _remoteDiscountServiceBaseUrl = settings.Value.DiscountUrl;
    }

    public async Task<HttpStatusCode> UpdateDiscountValueAsync(DiscountItem discountItem) {
        try {
            var uri = $"{_remoteDiscountServiceBaseUrl}discounts";

            // Serialize the discount item to JSON
            var discountItemJson = JsonConvert.SerializeObject(discountItem);
            using (StringContent requestContent = new StringContent(discountItemJson, Encoding.UTF8, "application/json")) {

                // Set the request timeout
                _httpClient.Timeout = TimeSpan.FromSeconds(10);

                var response = await _httpClient.PutAsync(uri, requestContent);
                if (response.StatusCode != HttpStatusCode.Created) {
                    _logger.LogError($"An error occurred while updating the discount item value. The response status code is {response.StatusCode}");
                }

                return response.StatusCode;
            }
        } catch (HttpRequestException ex) {
            _logger.LogError($"An error occurered while making the HTTP request: {ex.Message}");
            throw; // If needed, wrap the exception in a custom exception and throw it
        }
    }
}
