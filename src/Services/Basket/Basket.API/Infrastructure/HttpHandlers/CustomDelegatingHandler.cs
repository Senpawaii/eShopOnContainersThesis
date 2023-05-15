using System.Net.Http;
using System.Threading;

namespace Microsoft.eShopOnContainers.Services.Basket.API.Infrastructure.HttpHandlers;
public class CustomDelegatingHandler : DelegatingHandler {
    private readonly ILogger<CustomDelegatingHandler> _logger;

    public CustomDelegatingHandler(ILogger<CustomDelegatingHandler> logger) {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        // Perform logging or any other intercepting actions here
        _logger.LogInformation($"Intercepted request: {request.RequestUri}");

        return await base.SendAsync(request, cancellationToken);
    }
}
