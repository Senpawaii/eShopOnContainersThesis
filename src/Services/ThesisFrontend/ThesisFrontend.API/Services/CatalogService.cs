using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Text;

namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Services;
public class CatalogService : ICatalogService {
    private readonly HttpClient _httpClient;
    private readonly ILogger<CatalogService> _logger;

    private readonly string _remoteCatalogServiceBaseUrl;

    public CatalogService(HttpClient httpClient, ILogger<CatalogService> logger, IOptions<ThesisFrontendSettings> settings) {
        _httpClient = httpClient;
        _logger = logger;
        _remoteCatalogServiceBaseUrl = settings.Value.CatalogUrl;
    }

    public async Task<HttpStatusCode> UpdateCatalogPriceAsync(CatalogItem catalogItem) {
        try {
            var uri = $"{_remoteCatalogServiceBaseUrl}items";

            // Serialize the catalog item to JSON
            string catalogItemJson = JsonConvert.SerializeObject(catalogItem);
            using (StringContent requestContent = new StringContent(catalogItemJson, Encoding.UTF8, "application/json")) {
                
                // Set the request timeout
                _httpClient.Timeout = TimeSpan.FromSeconds(10);
                
                var response = await _httpClient.PutAsync(uri, requestContent);
                if (response.StatusCode != HttpStatusCode.Created) {
                    _logger.LogError($"An error occurred while updating the catalog item price. The response status code is {response.StatusCode}");
                }

                return response.StatusCode;
            }
        }
        catch (HttpRequestException ex) {
            _logger.LogError($"An error occurered while making the HTTP request: {ex.Message}");
            throw; // If needed, wrap the exception in a custom exception and throw it
        }

    }
}
