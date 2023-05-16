using Microsoft.eShopOnContainers.Services.Basket.API.DependencyServices;
using System.Net.Http;
using System.Threading;
using System.Web;

namespace Microsoft.eShopOnContainers.Services.Basket.API.Infrastructure.HttpHandlers;
public class CustomDelegatingHandler : DelegatingHandler {
    private readonly ILogger<CustomDelegatingHandler> _logger;
    private readonly IScopedMetadata _metadata;

    public CustomDelegatingHandler(ILogger<CustomDelegatingHandler> logger, IScopedMetadata metadata) {
        _logger = logger;
        _metadata = metadata;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        // Perform logging or any other intercepting actions here
        _logger.LogInformation($"Intercepted request: {request.RequestUri}");


        if (_metadata.ScopedMetadataFunctionalityID.Value != null) {
            _logger.LogInformation($"Functionality ID: {_metadata.ScopedMetadataFunctionalityID.Value} ");
            var uriBuilder = new UriBuilder(request.RequestUri);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["functionality_ID"] = _metadata.ScopedMetadataFunctionalityID.Value;
            query["interval_low"] = _metadata.ScopedMetadataLowInterval.ToString();
            query["interval_high"] = _metadata.ScopedMetadataHighInterval.ToString();
            query["timestamp"] = _metadata.ScopedMetadataTimestamp.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
            query["tokens"] = _metadata.ScopedMetadataTokens.Value.ToString();
            uriBuilder.Query = query.ToString();
            request.RequestUri = uriBuilder.Uri;

            // Log the new URI
            _logger.LogInformation($"New URI: {request.RequestUri}");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
