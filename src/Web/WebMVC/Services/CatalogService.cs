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

    public async Task<(Catalog, (int, int))> GetCatalogItems(int page, int take, int? brand, int? type, (int, int) interval)
    {
        // Generate the URI string
        var uri = API.Catalog.GetAllCatalogItems(_remoteServiceBaseUrl, page, take, brand, type, interval);

        // Contact the address identified by the URI using HTTP GET request
        //var responseString = await _httpClient.GetStringAsync(uri);

        // Obtain the header parameters (metadata)
        HttpResponseMessage response = await _httpClient.GetAsync(uri);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();

        //// Obtain the header parameters (metadata)
        HttpHeaders headers = response.Headers;

        IEnumerable<string> headerIntervalLow;
        IEnumerable<string> headerIntervalHigh;
        int intervalLow;
        int intervalHigh;
        //IEnumerable<string> values;

        if (!headers.TryGetValues(("IntervalLow"), out headerIntervalLow)) {
            _logger.LogInformation("Couldn't retrieve interval information from response header.");
        }
        if (!headers.TryGetValues(("IntervalHigh"), out headerIntervalHigh)) {
            _logger.LogInformation("Couldn't retrieve interval information from response header.");
        }
        if(!int.TryParse(headerIntervalLow.FirstOrDefault(), out intervalLow)) {
            _logger.LogInformation("Couldn't retrieve interval information from response header.");
        }
        if(!int.TryParse(headerIntervalHigh.FirstOrDefault(), out intervalHigh)) {
            _logger.LogInformation("Couldn't retrieve interval information from response header.");
        }
        

        //if (!int.TryParse(headerValues[0], out interval.Item1))
        //new_interval[0] = 1;

        //if (!int.TryParse(headerValues.FirstOrDefault(), out new_interval.Item1) && int.TryParse(headerValues.FirstOrDefault(), out new_interval.Item2)) {
        //    headerValues.
        //}

        // Decompose the responseString view in a Catalog object
        var catalog = JsonSerializer.Deserialize<Catalog>(responseString, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return (catalog, (intervalLow, intervalHigh));
    }

    public async Task<IEnumerable<SelectListItem>> GetBrands()
    {

        var uri = API.Catalog.GetAllBrands(_remoteServiceBaseUrl);

        var responseString = await _httpClient.GetStringAsync(uri);

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

        return items;
    }

    public async Task<IEnumerable<SelectListItem>> GetTypes()
    {
        var uri = API.Catalog.GetAllTypes(_remoteServiceBaseUrl);

        var responseString = await _httpClient.GetStringAsync(uri);

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

        return items;
    }
}
