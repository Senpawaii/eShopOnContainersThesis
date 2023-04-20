using System.Globalization;
using WebMVC.ViewModels;

namespace Microsoft.eShopOnContainers.WebMVC.Services;

public class CatalogService : ICatalogService
{
    private readonly IOptions<AppSettings> _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CatalogService> _logger;

    private readonly string _remoteServiceBaseUrl;

    public CatalogService(HttpClient httpClient, ILogger<CatalogService> logger, IOptions<AppSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;

        _remoteServiceBaseUrl = $"{_settings.Value.PurchaseUrl}/c/api/v1/catalog/";
    }

    public async Task<(Catalog, TCCMetadata)> GetCatalogItems(int page, int take, int? brand, int? type, TCCMetadata metadata) {
        // Generate the URI string
        var uri = API.Catalog.GetAllCatalogItems(_remoteServiceBaseUrl, page, take, brand, type, metadata);

        // Obtain the header parameters (metadata)
        HttpResponseMessage response = await _httpClient.GetAsync(uri);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();

        if(metadata != null) {
            ExtractMetadataFromResponseHeaders(metadata, response);
        }

        // Decompose the responseString view in a Catalog object
        var catalog = JsonSerializer.Deserialize<Catalog>(responseString, new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true
        });

        return (catalog, metadata);
    }

    public async Task<(IEnumerable<SelectListItem>, TCCMetadata)> GetBrands(TCCMetadata metadata)
    {

        var uri = API.Catalog.GetAllBrands(_remoteServiceBaseUrl, metadata);

        //var responseString = await _httpClient.GetStringAsync(uri);
        
        // Obtain the header parameters (metadata)
        HttpResponseMessage response = await _httpClient.GetAsync(uri);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();

        if (metadata != null) {
            ExtractMetadataFromResponseHeaders(metadata, response);
        }
        var items = new List<SelectListItem>();

        items.Add(new SelectListItem() { Value = null, Text = "All", Selected = true });
            
        using var brands = JsonDocument.Parse(responseString);

        foreach (JsonElement brand  in brands.RootElement.EnumerateArray())
        {
            items.Add(new SelectListItem()
            {
                Value = brand.GetProperty("id").ToString(),
                Text = brand.GetProperty("brand").ToString()
            });
        }

        return (items, metadata);
    }

    public async Task<(IEnumerable<SelectListItem>, TCCMetadata)> GetTypes(TCCMetadata metadata)
    {
        var uri = API.Catalog.GetAllTypes(_remoteServiceBaseUrl, metadata);

        //var responseString = await _httpClient.GetStringAsync(uri);

        // Obtain the header parameters (metadata)
        HttpResponseMessage response = await _httpClient.GetAsync(uri);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();

        if (metadata != null) {
            ExtractMetadataFromResponseHeaders(metadata, response);
        }
        var items = new List<SelectListItem>();
        items.Add(new SelectListItem() { Value = null, Text = "All", Selected = true });
            
        using var catalogTypes = JsonDocument.Parse(responseString);

        foreach (JsonElement catalogType in catalogTypes.RootElement.EnumerateArray())
        {
            items.Add(new SelectListItem()
            {
                Value = catalogType.GetProperty("id").ToString(),
                Text = catalogType.GetProperty("type").ToString()
            });
        }

        return (items, metadata);
    }

    private void ExtractMetadataFromResponseHeaders(TCCMetadata metadata, HttpResponseMessage response) {
        //// Obtain the header parameters (metadata) from the response
        HttpHeaders headers = response.Headers;

        IEnumerable<string> headerIntervalLow;
        IEnumerable<string> headerIntervalHigh;
        IEnumerable<string> headerTimestamp;
        int intervalLow;
        int intervalHigh;

        if (!headers.TryGetValues(("interval_low"), out headerIntervalLow)) {
            _logger.LogInformation("Couldn't retrieve interval information from response header.");
        }
        if (!headers.TryGetValues(("interval_high"), out headerIntervalHigh)) {
            _logger.LogInformation("Couldn't retrieve interval information from response header.");
        }
        if (!headers.TryGetValues(("timestamp"), out headerTimestamp)) {
            _logger.LogInformation("Couldn't retrieve timestamp information from response header.");
        }

        if (!int.TryParse(headerIntervalLow.FirstOrDefault(), out intervalLow)) {
            _logger.LogInformation("Couldn't retrieve interval information from response header.");
        }
        if (!int.TryParse(headerIntervalHigh.FirstOrDefault(), out intervalHigh)) {
            _logger.LogInformation("Couldn't retrieve interval information from response header.");
        }

        metadata.Interval = Tuple.Create(intervalLow, intervalHigh);
        metadata.Timestamp = DateTime.ParseExact(headerTimestamp.FirstOrDefault(), "yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
    }
}
