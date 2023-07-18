using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.DependencyServices;
using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Services;
//using NewRelic.Api.Agent;

namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Middleware;
public class TCCMiddleware {
    private readonly ILogger<TCCMiddleware> _logger;
    private readonly RequestDelegate _next;
    private IScopedMetadata _request_metadata;
    private ITokensContextSingleton _functionalitySingleton;
    private readonly ICoordinatorService _coordinatorSvc;

    public TCCMiddleware(ILogger<TCCMiddleware> logger, RequestDelegate next, ICoordinatorService coordinatorSvc, IScopedMetadata metadata, ITokensContextSingleton functionalitySingleton) {
        _logger = logger;
        _next = next;
        _request_metadata = metadata;
        _functionalitySingleton = functionalitySingleton;
        _coordinatorSvc = coordinatorSvc;
    }

   //[Trace]
    public async Task Invoke(HttpContext httpctx) {
        string currentUri = httpctx.Request.GetUri().ToString();

        if (currentUri.Contains("commit")) {
            await HandleCommitProtocol(httpctx);
            return;
        }

        // Initialize the metadata fields
        SeedMetadata();
        _logger.LogInformation($"ClientID: {_request_metadata.ClientID.Value}, currentUri: {currentUri}");

        // Add a new ManualResetEvent associated with the clientID
        _functionalitySingleton.AddManualResetEvent(_request_metadata.ClientID.Value);
        
        await _next.Invoke(httpctx);

        if (_functionalitySingleton.GetRemainingTokens(_request_metadata.ClientID.Value) > 0) {
            // Send the rest of the tokens to the coordinator
            await _coordinatorSvc.SendTokens();
        }

        // Clean the singleton fields for the current session context
        _functionalitySingleton.RemoveRemainingTokens(_request_metadata.ClientID.Value);

        if(!currentUri.Contains("updatepricediscount")) {
            // This is a readonly request, no need to wait for the transaction to complete
            _functionalitySingleton.RemoveTransactionState(_request_metadata.ClientID.Value);
            _functionalitySingleton.RemoveManualResetEvent(_request_metadata.ClientID.Value);
            return;
        }

        // Block the result until the state of the transaction is set to either commit or abort, timeout after 5 seconds
        var mre = _functionalitySingleton.GetManualResetEvent(_request_metadata.ClientID.Value);
        var success = mre.WaitOne();

        // Clean the singleton fields for the current session context
        _functionalitySingleton.RemoveTransactionState(_request_metadata.ClientID.Value);

        _logger.LogInformation($"Transaction for {_request_metadata.ClientID.Value} was completed successfully");
        _functionalitySingleton.RemoveManualResetEvent(_request_metadata.ClientID.Value);
        return;
    }

   //[Trace]
    private async Task HandleCommitProtocol(HttpContext httpctx) {
        if (httpctx.Request.Query.TryGetValue("clientID", out var clientID)) {
            _request_metadata.ClientID.Value = clientID;
            _logger.LogInformation($"ClientID: {clientID}, Committing... Signal ManualResetEvent");
            _functionalitySingleton.SignalManualResetEvent(clientID);
        }
        else {
            _logger.LogError("ClientID not found in the request");
            await _next.Invoke(httpctx);
            return;
        }
        if (httpctx.Request.Query.TryGetValue("state", out var state)) {
            // Change the state of the transaction associated with the clientID
            ChangeTransactionState(clientID, state);
            await _next.Invoke(httpctx);
            return;
        }
        else {
            _logger.LogError("State not found in the request");
            await _next.Invoke(httpctx);
            return;
        }
    }

   //[Trace]
    private void ChangeTransactionState(string clientID, string state) {
        _functionalitySingleton.ChangeTransactionState(clientID, state);
    }

    private void SeedMetadata() {
        // Generate a 32 bit random string for the client ID
        string clientID = GenerateRandomString(32);

        // Generate a timestamp for the request
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");

        // Generate tokens
        int tokens = 1000000000;
        _functionalitySingleton.AddRemainingTokens(clientID, tokens);
        _request_metadata.Tokens.Value = tokens;
        _request_metadata.Timestamp.Value = timestamp;
        _request_metadata.ClientID.Value = clientID;
        _request_metadata.ReadOnly.Value = true;
    }

   //[Trace]
    private string GenerateRandomString(int length) {
    Random random = new Random();
    const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, length)
                       .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}

public static class TCCMiddlewareExtension {
    public static IApplicationBuilder UseTCCMiddleware(this IApplicationBuilder app) {
        return app.UseMiddleware<TCCMiddleware>();
    }
}
