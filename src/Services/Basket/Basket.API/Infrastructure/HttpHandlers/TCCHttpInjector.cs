using Microsoft.eShopOnContainers.Services.Basket.API.DependencyServices;
using System.Net.Http;
using System.Threading;
using System.Web;

namespace Microsoft.eShopOnContainers.Services.Basket.API.Infrastructure.HttpHandlers;
public class TCCHttpInjector : DelegatingHandler {
    private readonly ILogger<TCCHttpInjector> _logger;
    private readonly IScopedMetadata _metadata;
    private readonly ITokensContextSingleton _remainingTokens;

    public TCCHttpInjector(ILogger<TCCHttpInjector> logger, IScopedMetadata metadata, ITokensContextSingleton remainingTokens) {
        _logger = logger;
        _metadata = metadata;
        _remainingTokens = remainingTokens;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        if (_metadata.ClientID.Value != null) {
            // _logger.LogInformation($"ClientID: {_metadata.ClientID.Value} ");
            
            var uriBuilder = new UriBuilder(request.RequestUri);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["clientID"] = _metadata.ClientID.Value;
            query["timestamp"] = _metadata.Timestamp.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");

            string clientID = _metadata.ClientID.Value;
            int remainingTokensSession = _remainingTokens.GetRemainingTokens(clientID);
            int partialTokensToSend = remainingTokensSession / 2;
            _remainingTokens.DecrementRemainingTokens(clientID, partialTokensToSend);

            _metadata.Tokens.Value -= partialTokensToSend;

            query["tokens"] = partialTokensToSend.ToString();
            uriBuilder.Query = query.ToString();
            request.RequestUri = uriBuilder.Uri;
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
