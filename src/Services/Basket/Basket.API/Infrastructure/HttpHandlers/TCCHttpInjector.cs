using Microsoft.eShopOnContainers.Services.Basket.API.DependencyServices;
using System.Net.Http;
using System.Threading;
using System.Web;

namespace Microsoft.eShopOnContainers.Services.Basket.API.Infrastructure.HttpHandlers;
public class TCCHttpInjector : DelegatingHandler {
    private readonly ILogger<TCCHttpInjector> _logger;
    private readonly IScopedMetadata _metadata;

    public TCCHttpInjector(ILogger<TCCHttpInjector> logger, IScopedMetadata metadata) {
        _logger = logger;
        _metadata = metadata;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        if (_metadata.ClientID.Value != null) {
            _logger.LogInformation($"ClientID: {_metadata.ClientID.Value} ");
            
            var uriBuilder = new UriBuilder(request.RequestUri);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["clientID"] = _metadata.ClientID.Value;
            query["timestamp"] = _metadata.Timestamp.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
            
            var partialTokensToSend = _metadata.Tokens.Value / 2;
            _metadata.Tokens.Value = _metadata.Tokens.Value - partialTokensToSend;

            query["tokens"] = partialTokensToSend.ToString();
            uriBuilder.Query = query.ToString();
            request.RequestUri = uriBuilder.Uri;
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
