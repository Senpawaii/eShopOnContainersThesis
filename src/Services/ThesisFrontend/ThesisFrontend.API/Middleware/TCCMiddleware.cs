using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.DependencyServices;
using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Services;
using NewRelic.Api.Agent;

namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Middleware;
public class TCCMiddleware {
    private readonly ILogger<TCCMiddleware> _logger;
    private readonly RequestDelegate _next;
    private IScopedMetadata _request_metadata;
    private ITokensContextSingleton _remainingTokens;
    private readonly ICoordinatorService _coordinatorSvc;

    public TCCMiddleware(ILogger<TCCMiddleware> logger, RequestDelegate next, ICoordinatorService coordinatorSvc, IScopedMetadata metadata, ITokensContextSingleton remainingTokens) {
        _logger = logger;
        _next = next;
        _request_metadata = metadata;
        _remainingTokens = remainingTokens;
        _coordinatorSvc = coordinatorSvc;
    }

    [Trace]
    public async Task Invoke(HttpContext httpctx) {
        string currentUri = httpctx.Request.GetUri().ToString();

        if (currentUri.Contains("commit")) {
            await HandleCommitProtocol(httpctx);
            return;
        }

        // Initialize the metadata fields
        SeedMetadata();
        
        // Log the current Time and the client ID
        //_logger.LogInformation($"Sending Request at {DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt", CultureInfo.InvariantCulture)} for functionality {_request_metadata.ClientID.Value}.");

        await _next.Invoke(httpctx);

        // Log the current Time and the client ID
        //_logger.LogInformation($"TF1 at {DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt", CultureInfo.InvariantCulture)} for functionality {_request_metadata.ClientID.Value}.");


        // Send the rest of the tokens to the coordinator
        if (_remainingTokens.GetRemainingTokens(_request_metadata.ClientID.Value) > 0) {
            await _coordinatorSvc.SendTokens();
        }

        // Log the current Time and the client ID
        //_logger.LogInformation($"Finishing Request at {DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt", CultureInfo.InvariantCulture)} for functionality {_request_metadata.ClientID.Value}.");

        // Clean the singleton fields for the current session context
        _remainingTokens.RemoveRemainingTokens(_request_metadata.ClientID.Value);

        if(!currentUri.Contains("updatepricediscount")) {
            return;
        }

        // Block the result until the state of the transaction is set to either commit or abort, timeout after 5 seconds
        var currentTime = DateTime.Now;
        while (_remainingTokens.GetTransactionState(_request_metadata.ClientID.Value) == null) {
            // _logger.LogInformation($"Waiting for the transaction state to be set for {_request_metadata.ClientID.Value}");
            await Task.Delay(10);
            if (DateTime.Now.Subtract(currentTime).TotalSeconds > 5) {
                _logger.LogError($"Timeout while waiting for the transaction state to be set for {_request_metadata.ClientID.Value}");
                // Set the transaction state to abort
                _remainingTokens.ChangeTransactionState(_request_metadata.ClientID.Value, "abort");
                break;
            }
        }

        // Check if the transaction was aborted
        if (_remainingTokens.GetTransactionState(_request_metadata.ClientID.Value) == "abort") {
            // Clean the singleton fields for the current session context
            _remainingTokens.RemoveTransactionState(_request_metadata.ClientID.Value);
            // Return the abort signal to the client
            httpctx.Response.StatusCode = 409;
            return;
        }
        // Clean the singleton fields for the current session context
        _remainingTokens.RemoveTransactionState(_request_metadata.ClientID.Value);

    }

    [Trace]
    private async Task HandleCommitProtocol(HttpContext httpctx) {
        if (httpctx.Request.Query.TryGetValue("clientID", out var clientID)) {
            _request_metadata.ClientID.Value = clientID;
            _logger.LogInformation($"Committing transaction for {_request_metadata.ClientID.Value}");
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

    [Trace]
    private void ChangeTransactionState(string clientID, string state) {
        _remainingTokens.ChangeTransactionState(clientID, state);
    }

    private void SeedMetadata() {
        // Generate a 32 bit random string for the client ID
        string clientID = GenerateRandomString(32);

        // Generate a timestamp for the request
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");

        // Generate tokens
        int tokens = 1000000000;
        _remainingTokens.AddRemainingTokens(clientID, tokens);
        _request_metadata.Tokens.Value = tokens;
        _request_metadata.Timestamp.Value = timestamp;
        _request_metadata.ClientID.Value = clientID;
        _request_metadata.ReadOnly.Value = true;
    }

    [Trace]
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
