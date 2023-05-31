namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Middleware;
public class TCCMiddleware {
    private readonly ILogger<TCCMiddleware> _logger;
    private readonly RequestDelegate _next;

    public TCCMiddleware(ILogger<TCCMiddleware> logger, RequestDelegate next) {
        _logger = logger;
        _next = next;
    }

    public async Task Invoke(HttpContext httpctx) {
        await _next.Invoke(httpctx);
    }
}

public static class TCCMiddlewareExtension {
    public static IApplicationBuilder UseTCCMiddleware(this IApplicationBuilder app) {
        return app.UseMiddleware<TCCMiddleware>();
    }
}
