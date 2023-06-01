using Microsoft.eShopOnContainers.Services.Discount.API.Services;
using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.DependencyServices;

namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Middleware;
public class TCCMiddleware {
    private readonly ILogger<TCCMiddleware> _logger;
    private readonly RequestDelegate _next;
    private static Random random = new Random();

    public TCCMiddleware(ILogger<TCCMiddleware> logger, RequestDelegate next) {
        _logger = logger;
        _next = next;
    }

    public async Task Invoke(HttpContext httpctx, IScopedMetadata metadata, ICoordinatorService coordinatorSvc) {
        // Initialize the metadata fields
        SeedMetadata(metadata);

        await _next.Invoke(httpctx);

        // Send the rest of the tokens to the coordinator
        if(metadata.Tokens.Value > 0) {
            await coordinatorSvc.SendTokens();
        }
    }
    private void SeedMetadata(IScopedMetadata metadata) {
        // Generate a 32 bit random string for the client ID
        string clientID = GenerateRandomString(32);

        // Generate a timestamp for the request
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");

        // Generate tokens
        int tokens = 1000000000;

        metadata.Tokens.Value = tokens;
        metadata.Timestamp.Value = timestamp;
        metadata.ClientID.Value = clientID;
    }

    private string GenerateRandomString(int length) {
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
