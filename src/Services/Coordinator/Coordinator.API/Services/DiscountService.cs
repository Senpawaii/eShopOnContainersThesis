using Microsoft.Extensions.Options;
using System.Net.Http;

namespace Microsoft.eShopOnContainers.Services.Coordinator.API.Services;
public class DiscountService : IDiscountService {
    private readonly IOptions<CoordinatorSettings> _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<DiscountService> _logger;

    private readonly string _remoteServiceBaseUrl;

    public DiscountService(HttpClient httpClient, ILogger<DiscountService> logger, IOptions<CoordinatorSettings> settings) {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings;

        _remoteServiceBaseUrl = settings.Value.DiscountUrl;
    }

    public async Task IssueCommit(string maxTS, string funcID) {
        string uri = $"{_settings.Value.DiscountUrl}commit?timestamp={maxTS}&functionality_ID={funcID}";

        HttpResponseMessage response = await _httpClient.GetAsync(uri);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
    }
}
