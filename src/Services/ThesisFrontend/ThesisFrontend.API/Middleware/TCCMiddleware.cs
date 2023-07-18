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
            // We are handling a write request commit signal from the coordinator
            await HandleCommitProtocol(httpctx);
            return;
        }

        // Else, we are handling a read request or the beginning of a write request
        // Initialize the metadata fields
        SeedMetadata();
        _logger.LogInformation($"ClientID: {_request_metadata.ClientID.Value}, currentUri: {currentUri} at {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");

        // Add a new ManualResetEvent associated with the clientID if we are handling a write request
        if( currentUri.Contains("updatepricediscount")) {
            _functionalitySingleton.AddManualResetEvent(_request_metadata.ClientID.Value);
        }
        
        // Execute the next middleware in the pipeline (the controller)
        await _next.Invoke(httpctx);

        // Send the rest of the tokens to the coordinator
        await _coordinatorSvc.SendTokens();

        // Clean the singleton fields for the current session context
        _functionalitySingleton.RemoveRemainingTokens(_request_metadata.ClientID.Value);

        if( currentUri.Contains("updatepricediscount")) {
            // Block the result until the transaction is ready to be committed (and return the result of the write transaction to the client)
            var mre = _functionalitySingleton.GetManualResetEvent(_request_metadata.ClientID.Value);
            var success = mre.WaitOne();

            // Clean the singleton fields for the current session context
            _functionalitySingleton.RemoveTransactionState(_request_metadata.ClientID.Value);
            _functionalitySingleton.RemoveManualResetEvent(_request_metadata.ClientID.Value);

            _logger.LogInformation($"ClientID: {_request_metadata.ClientID.Value}, WRITE transaction completed at {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");
        }
        else {
            _logger.LogInformation($"ClientID: {_request_metadata.ClientID.Value}, READ transaction ({currentUri}) completed at {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");
        }
        return;
    }

   //[Trace]
    private async Task HandleCommitProtocol(HttpContext httpctx) {
        if (httpctx.Request.Query.TryGetValue("clientID", out var clientID)) {
            _request_metadata.ClientID.Value = clientID;
            _logger.LogInformation($"ClientID: {clientID}, Committing... Signal ManualResetEvent at {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");
            _functionalitySingleton.SignalManualResetEvent(clientID);
        }
        else {
            _logger.LogError("ClientID not found in the request");
            await _next.Invoke(httpctx);
            return;
        }
        await _next.Invoke(httpctx);
        return;
    }

    private void SeedMetadata() {
        // Generate a 32 bit random string for the client ID
        string clientID = GenerateRandomString(32);

        // Generate a timestamp for the request from the current UTC time minus 1 second
        // string timestamp = DateTime.UtcNow.AddSeconds(-1).ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
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
