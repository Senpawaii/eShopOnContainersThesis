using Microsoft.eShopOnContainers.Services.Coordinator.API;
using Microsoft.Extensions.Options;
using System.Net.Http;

namespace Coordinator.API.Services {
    public class BasketService : IBasketService {
        private readonly IOptions<CoordinatorSettings> _settings;
        private readonly HttpClient _httpClient;
        private readonly ILogger<BasketService> _logger;

        public BasketService(HttpClient httpClient, ILogger<BasketService> logger, IOptions<CoordinatorSettings> options) { 
            _httpClient = httpClient;
            _logger = logger;
            _settings = options;
        }

        public async Task IssueCommitEventBased(string clientID) {
            string uri = $"{_settings.Value.BasketUrl}confirm?clientID={clientID}";

            HttpResponseMessage response = await _httpClient.GetAsync(uri);

            var responseString = await response.Content.ReadAsStringAsync();
        }
    }
}
