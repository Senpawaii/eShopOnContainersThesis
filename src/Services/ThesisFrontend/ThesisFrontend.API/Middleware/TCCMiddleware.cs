using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.DependencyServices;
using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Services;

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

    public async Task Invoke(HttpContext httpctx) {
        // Initialize the metadata fields
        SeedMetadata();

        await _next.Invoke(httpctx);

        // Send the rest of the tokens to the coordinator
        if (_remainingTokens.GetRemainingTokens(_request_metadata.ClientID.Value) > 0) {
            await _coordinatorSvc.SendTokens();
        }
        // Clean the singleton fields for the current session context
        _remainingTokens.RemoveRemainingTokens(_request_metadata.ClientID.Value);
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
    }

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
