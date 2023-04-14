using System.Collections.Concurrent;
using static System.Net.Mime.MediaTypeNames;

namespace Microsoft.eShopOnContainers.Services.Discount.API.Middleware {
    public class TCCMiddleware {
        private readonly ILogger<TCCMiddleware> _logger;
        private readonly RequestDelegate _next;

        public TCCMiddleware(ILogger<TCCMiddleware> logger, RequestDelegate next) {
            _logger = logger;
            _next = next;
        }

        // Middleware has access to Scoped Data, dependency-injected at Startup
        public async Task Invoke(HttpContext ctx) {
            // Call the next middleware
            await _next.Invoke(ctx);

        }
    }

    public static class TCCMiddlewareExtension {
        public static IApplicationBuilder UseTCCMiddleware(this IApplicationBuilder app) {
            return app.UseMiddleware<TCCMiddleware>(); 
        }
    }
}
