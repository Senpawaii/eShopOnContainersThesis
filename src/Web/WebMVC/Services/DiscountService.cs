using System.Globalization;
using WebMVC.ViewModels;

namespace Microsoft.eShopOnContainers.WebMVC.Services; 
public class DiscountService : IDiscountService {
    private readonly IOptions<AppSettings> _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<DiscountService> _logger;

    private readonly string _remoteServiceBaseUrl;

    public DiscountService(HttpClient httpClient, ILogger<DiscountService> logger, IOptions<AppSettings> settings) {
        _settings = settings;
        _httpClient = httpClient;
        _logger = logger;
        _remoteServiceBaseUrl = $"{_settings.Value.PurchaseUrl}/d/api/v1/discount/"; // Not sure what value should come here
    }

    public async Task<(IEnumerable<Discount>, TCCMetadata)> GetDiscountsById(List<int> itemIds, TCCMetadata metadata) {
        // Generate the URI string
        var uri = API.Discount.GetDiscountsById(_remoteServiceBaseUrl, itemIds, metadata);

        HttpResponseMessage response = await _httpClient.GetAsync(uri);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();

        if(metadata != null) {
            // Skip for now, until the Discount service has the metadata fields working
            //ExtractMetadataFromResponseHeaders(metadata, response); 
        }

        // Decompose the responseString view in a List of Discount objects
        var discounts = JsonSerializer.Deserialize<List<Discount>>(responseString, new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true
        });

        return (discounts, metadata);
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
