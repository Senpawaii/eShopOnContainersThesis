using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.DependencyServices;
using System.Net.Http;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
using System.Web;

namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Infrastructure.HttpHandlers;
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
        _logger.LogDebug($"ClientID: {_metadata.ClientID.Value}");

        var uriBuilder = new UriBuilder(request.RequestUri);
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);
        query["clientID"] = _metadata.ClientID.Value;
        query["timestamp"] = _metadata.Timestamp.Value;

        string clientID = _metadata.ClientID.Value;
        int remainingTokensSession = _remainingTokens.GetRemainingTokens(clientID);
        int partialTokensToSend = remainingTokensSession / 2;
        _remainingTokens.DecrementRemainingTokens(clientID, partialTokensToSend);

        query["tokens"] = partialTokensToSend.ToString();
        uriBuilder.Query = query.ToString();
        request.RequestUri = uriBuilder.Uri;

        _logger.LogInformation($"TF HTTP Injector at {DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt", CultureInfo.InvariantCulture)} for functionality {clientID}.");

        return await base.SendAsync(request, cancellationToken);
    }
}
