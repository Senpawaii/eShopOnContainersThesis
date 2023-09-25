using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Text;
//using NewRelic.Api.Agent;

namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Services;
public class DiscountService : IDiscountService {
    private readonly HttpClient _httpClient;
    private readonly ILogger<DiscountService> _logger;

    private readonly string _remoteDiscountServiceBaseUrl;

    public DiscountService(HttpClient httpClient, ILogger<DiscountService> logger, IOptions<ThesisFrontendSettings> settings) { 
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(100);
        _logger = logger;
        _remoteDiscountServiceBaseUrl = settings.Value.DiscountUrl;
    }

   //[Trace]
    public async Task<IEnumerable<DiscountItem>> GetDiscountItemsAsync(List<string> itemNames, List<string> itemBrands, List<string> itemTypes) {
        try {
            // Make sure the items Names that include "&" are encoded correctly as "%26"
            for (int i = 0; i < itemNames.Count; i++) {
                itemNames[i] = itemNames[i].Replace("&", "%26");
            }

            var uri = $"{_remoteDiscountServiceBaseUrl}discounts?itemNames={string.Join(",", itemNames)}&itemBrands={string.Join(",", itemBrands)}&itemTypes={string.Join(",", itemTypes)}";

            // Log the request URI
            // _logger.LogInformation($"Sending request to {_remoteDiscountServiceBaseUrl}discounts?itemNames={string.Join(",", itemNames)}&itemBrands={string.Join(",", itemBrands)}&itemTypes={string.Join(",", itemTypes)}");

            var response = await _httpClient.GetAsync(uri);
            if (response.StatusCode != HttpStatusCode.OK) {
                _logger.LogError($"An error occurred while getting the discount items. The response status code is {response.StatusCode}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            var discountItems = JsonConvert.DeserializeObject<IEnumerable<DiscountItem>>(responseString);

            return discountItems;
        } catch (HttpRequestException ex) {
            _logger.LogError($"An error occurered while making the HTTP request: {ex.Message}");
            throw; // If needed, wrap the exception in a custom exception and throw it
        } catch (TaskCanceledException) {
            // Handle the timeout exception here, or log it
            Console.WriteLine("HTTP request was canceled due to a timeout.");
            return null;
        }
    }

   //[Trace]
    public async Task<HttpStatusCode> UpdateDiscountValueAsync(DiscountItem discountItem) {
        try {
            var uri = $"{_remoteDiscountServiceBaseUrl}discounts";

            // Serialize the discount item to JSON
            var discountItemJson = JsonConvert.SerializeObject(discountItem);
            using (StringContent requestContent = new StringContent(discountItemJson, Encoding.UTF8, "application/json")) {
                var response = new HttpResponseMessage();
                try {
                    response = await _httpClient.PutAsync(uri, requestContent);
                } catch (HttpRequestException ex) {
                    _logger.LogError($"An error occurred while updating the discount item value. The HTTP request failed with exception message: {ex.Message}");
                    return HttpStatusCode.InternalServerError;
                }

                if (response.StatusCode != HttpStatusCode.Created) {
                    _logger.LogError($"An error occurred while updating the discount item value. The response status code is {response.StatusCode}");
                    return HttpStatusCode.InternalServerError;
                }

                return response.StatusCode;
            }
        } catch (Exception ex) {
            _logger.LogError($"An error occurered while making the HTTP request: {ex.Message}, uri={_remoteDiscountServiceBaseUrl}discounts");
            return HttpStatusCode.InternalServerError;
        } catch (TaskCanceledException) {
            // Handle the timeout exception here, or log it
            Console.WriteLine("HTTP request was canceled due to a timeout.");
            return null;
        }
    }
}
