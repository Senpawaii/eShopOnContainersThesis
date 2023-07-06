using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Text;
//using NewRelic.Api.Agent;

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

   //[Trace]
    public async Task<CatalogItem> GetCatalogItemByIdAsync(int catalogItemId) {
        try {
            var uri = $"{_remoteCatalogServiceBaseUrl}items/{catalogItemId}";

            // Set the request timeout
            _httpClient.Timeout = TimeSpan.FromSeconds(10);

            var response = await _httpClient.GetAsync(uri);
            if (response.StatusCode != HttpStatusCode.OK) {
                if(response.StatusCode == HttpStatusCode.NotFound) {
                    _logger.LogError($"The catalog item with id {catalogItemId} was not found");
                    return null;
                }
                else if(response.StatusCode == HttpStatusCode.BadRequest) {
                    _logger.LogError($"The catalog item id {catalogItemId} is not valid");
                    return null;
                }
            }

            var responseString = await response.Content.ReadAsStringAsync();

            if(string.IsNullOrEmpty(responseString)) {
                _logger.LogError($"An error occurred while getting the catalog item. The response string is empty");
                return null;
            }
            var catalogItem = JsonConvert.DeserializeObject<CatalogItem>(responseString);

            return catalogItem;
        }
        catch (HttpRequestException ex) {
            _logger.LogError($"An error occurered while making the HTTP request: {ex.Message}");
            throw; // If needed, wrap the exception in a custom exception and throw it
        }
    }

   //[Trace]
    public async Task<IEnumerable<CatalogBrand>> GetCatalogBrandsAsync() {
        try {
            var uri = $"{_remoteCatalogServiceBaseUrl}catalogbrands";

            // Set the request timeout
            _httpClient.Timeout = TimeSpan.FromSeconds(10);

            var response = await _httpClient.GetAsync(uri);
            if (response.StatusCode != HttpStatusCode.OK) {
                _logger.LogError($"An error occurred while getting the catalog brands. The response status code is {response.StatusCode}");
                return null;
            }

            var responseString = await response.Content.ReadAsStringAsync();

            if(string.IsNullOrEmpty(responseString)) {
                _logger.LogError($"An error occurred while getting the catalog brands. The response string is empty");
                return null;
            }
            var catalogBrands = JsonConvert.DeserializeObject<IEnumerable<CatalogBrand>>(responseString);

            return catalogBrands;
        }
        catch (HttpRequestException ex) {
            _logger.LogError($"An error occurered while making the HTTP request: {ex.Message}");
            throw; // If needed, wrap the exception in a custom exception and throw it
        }
    }

   //[Trace]
    public async Task<IEnumerable<CatalogType>> GetCatalogTypesAsync() {
        try {
            var uri = $"{_remoteCatalogServiceBaseUrl}catalogtypes";

            // Set the request timeout
            _httpClient.Timeout = TimeSpan.FromSeconds(10);

            var response = await _httpClient.GetAsync(uri);
            if (response.StatusCode != HttpStatusCode.OK) {
                _logger.LogError($"An error occurred while getting the catalog types. The response status code is {response.StatusCode}");
                return null;
            }

            var responseString = await response.Content.ReadAsStringAsync();

            if(string.IsNullOrEmpty(responseString)) {
                _logger.LogError($"An error occurred while getting the catalog types. The response string is empty");
                return null;
            }
            var catalogTypes = JsonConvert.DeserializeObject<IEnumerable<CatalogType>>(responseString);

            return catalogTypes;
        }
        catch (HttpRequestException ex) {
            _logger.LogError($"An error occurered while making the HTTP request: {ex.Message}");
            throw; // If needed, wrap the exception in a custom exception and throw it
        }
    }

   //[Trace]
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
