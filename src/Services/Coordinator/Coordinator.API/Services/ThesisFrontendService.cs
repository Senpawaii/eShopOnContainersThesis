using Microsoft.Extensions.Options;
using System.Net.Http;

namespace Microsoft.eShopOnContainers.Services.Coordinator.API.Services;
public class ThesisFrontendService : IThesisFrontendService {
    private readonly IOptions<CoordinatorSettings> _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ThesisFrontendService> _logger;

    private readonly string _remoteServiceBaseUrl;

    public ThesisFrontendService(HttpClient httpClient, ILogger<ThesisFrontendService> logger, IOptions<CoordinatorSettings> settings) {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings;

        _remoteServiceBaseUrl = settings.Value.ThesisFrontendUrl;
    }

    public async Task IssueCommit(string clientID) {
        string uri = $"{_remoteServiceBaseUrl}commit?clientID={clientID}&state=OK";
        // _logger.LogInformation($"URI being called:{uri}");
    
        HttpResponseMessage response = await _httpClient.GetAsync(uri);
        //response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();
    }
}
