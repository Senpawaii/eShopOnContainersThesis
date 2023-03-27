using System.Collections.Concurrent;
using static System.Net.Mime.MediaTypeNames;

namespace Microsoft.eShopOnContainers.Services.Catalog.API.Middleware {
    public class IntegerMiddleware {
        private readonly ILogger<IntegerMiddleware> _logger;
        private readonly RequestDelegate _next;
        private static ConcurrentBag<double> _testInts = new ConcurrentBag<double>();

        public IntegerMiddleware(ILogger<IntegerMiddleware> logger, RequestDelegate next) {
            _logger = logger;
            _next = next;
        }

        public async Task Invoke(HttpContext ctx) {
            if(ctx.Request.Query.TryGetValue("tokens", out var tokens)) {
                _testInts.Add(double.Parse(tokens));
                _logger.LogInformation($"Registered tokens: {tokens}");

                var removeTheseParams = new List<string> { "tokens" }.AsReadOnly();

                var filteredQueryParams = ctx.Request.Query.ToList().Where(filterKvp => !removeTheseParams.Contains(filterKvp.Key));
                var filteredQueryString = QueryString.Create(filteredQueryParams);
                ctx.Request.QueryString = filteredQueryString;
            }
            else {
                //_logger.LogInformation("The tokens parameter does not exist in the HTTP context.");
            }
            await _next.Invoke(ctx);
        }
    }

    public static class IntegerExtension {
        public static IApplicationBuilder UseInteger(this IApplicationBuilder app) {
            return app.UseMiddleware<IntegerMiddleware>(); 
        }
    }
}
