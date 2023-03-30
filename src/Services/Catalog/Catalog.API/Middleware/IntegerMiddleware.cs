using Catalog.API.DependencyServices;
using System.Collections.Concurrent;
using static System.Net.Mime.MediaTypeNames;

namespace Microsoft.eShopOnContainers.Services.Catalog.API.Middleware {
    public class IntegerMiddleware {
        private readonly ILogger<IntegerMiddleware> _logger;
        private readonly RequestDelegate _next;

        public IntegerMiddleware(ILogger<IntegerMiddleware> logger, RequestDelegate next) {
            _logger = logger;
            _next = next;
        }

        // Middleware has access to Scoped Data, dependency-injected at Startup
        public async Task Invoke(HttpContext ctx, IScopedMetadata svc) {
            if(ctx.Request.Query.TryGetValue("Interval_low", out var interval_lowStr) && ctx.Request.Query.TryGetValue("Interval_high", out var interval_highStr)) {
                //_testInts.Add(double.Parse(tokens));
                _logger.LogInformation($"Registered interval: {interval_lowStr}:{interval_highStr}");

                int interval_low;
                int interval_high;
                if(!int.TryParse(interval_lowStr, out interval_low)) {
                    _logger.LogInformation("Failed to parse Low Interval.");
                }
                if(!int.TryParse(interval_highStr, out interval_high)) {
                    _logger.LogInformation("Failed to parse High Interval.");
                }
                svc.ScopedMetadataIntervalLow = interval_low;
                svc.ScopedMetadataIntervalHigh = interval_high;

                var removeTheseParams = new List<string> { "Interval_low", "Interval_high" }.AsReadOnly();

                var filteredQueryParams = ctx.Request.Query.ToList().Where(filterKvp => !removeTheseParams.Contains(filterKvp.Key));
                var filteredQueryString = QueryString.Create(filteredQueryParams);
                ctx.Request.QueryString = filteredQueryString;
                
                // Store the original body stream for restoring the response body back its original stream
                Stream originalResponseBody = ctx.Response.Body;

                // Create a new memory stream for reading the response; Response body streams are write-only, therefore memory stream is needed here to read
                await using var memStream = new MemoryStream();
                ctx.Response.Body = memStream;

                ctx.Response.OnStarting(() => {
                    ctx.Response.Headers["IntervalLow"] = svc.ScopedMetadataIntervalLow.ToString();
                    ctx.Response.Headers["IntervalHigh"] = svc.ScopedMetadataIntervalHigh.ToString();

                    return Task.CompletedTask;
                });
                
                // Call the next middleware
                await _next.Invoke(ctx);

                // Set stream pointer position to 0 before reading
                memStream.Seek(0, SeekOrigin.Begin);

                // Read the body from the stream
                var responseBodyText = await new StreamReader(memStream).ReadToEndAsync();

                // Reset the position to 0 after reading
                memStream.Seek(0, SeekOrigin.Begin);

                // Do this last, that way you can ensure that the end results end up in the response.
                // (This resulting response may come either from the redirected route or other special routes if you have any redirection/re-execution involved in the middleware.)
                // This is very necessary. ASP.NET doesn't seem to like presenting the contents from the memory stream.
                // Therefore, the original stream provided by the ASP.NET Core engine needs to be swapped back.
                // Then write back from the previous memory stream to this original stream.
                // (The content is written in the memory stream at this point; it's just that the ASP.NET engine refuses to present the contents from the memory stream.)
                ctx.Response.Body = originalResponseBody;
                await ctx.Response.Body.WriteAsync(memStream.ToArray());
            } 
            else if(ctx.Request.Query.TryGetValue("Tokens", out var tokens)) {
                _logger.LogInformation($"Registered tokens: {tokens}");

                svc.ScopedMetadataTokens = double.Parse(tokens);

            }
            else {
                //_logger.LogInformation("The tokens parameter does not exist in the HTTP context.");
                await _next.Invoke(ctx);
            }
            
        }
    }

    public static class IntegerExtension {
        public static IApplicationBuilder UseInteger(this IApplicationBuilder app) {
            return app.UseMiddleware<IntegerMiddleware>(); 
        }
    }
}
