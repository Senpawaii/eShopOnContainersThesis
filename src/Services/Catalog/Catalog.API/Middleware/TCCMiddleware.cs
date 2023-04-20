using Catalog.API.DependencyServices;
using System.Collections.Concurrent;
using static System.Net.Mime.MediaTypeNames;

namespace Microsoft.eShopOnContainers.Services.Catalog.API.Middleware {
    public class TCCMiddleware {
        private readonly ILogger<TCCMiddleware> _logger;
        private readonly RequestDelegate _next;

        public TCCMiddleware(ILogger<TCCMiddleware> logger, RequestDelegate next) {
            _logger = logger;
            _next = next;
        }

        // Middleware has access to Scoped Data, dependency-injected at Startup
        public async Task Invoke(HttpContext ctx, IScopedMetadata svc) {

            // To differentiate from a regular call, check for the functionality ID
            if (ctx.Request.Query.TryGetValue("functionality_ID", out var functionality_ID)) {
                svc.ScopedMetadataFunctionalityID = functionality_ID;


                // Check for the other parameters and remove them as needed
                if (ctx.Request.Query.TryGetValue("interval_low", out var interval_lowStr) &&
                    ctx.Request.Query.TryGetValue("interval_high", out var interval_highStr)) {

                    _logger.LogInformation($"Registered interval: {interval_lowStr}:{interval_highStr}");

                    if (int.TryParse(interval_lowStr, out var interval_low)) {
                        svc.ScopedMetadataIntervalLow = interval_low;
                    }
                    else {
                        _logger.LogInformation("Failed to parse Low Interval.");
                    }
                    if (int.TryParse(interval_highStr, out var interval_high)) {
                        svc.ScopedMetadataIntervalHigh = interval_high;
                    }
                    else {
                        _logger.LogInformation("Failed to parse High Interval.");
                    }
                }

                if(ctx.Request.Query.TryGetValue("timestamp", out var timestamp)) {
                    _logger.LogInformation($"Registered timestamp: {timestamp}");
                    svc.ScopedMetadataTimestamp = DateTime.ParseExact(timestamp, "yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
                }

                var removeTheseParams = new List<string> { "interval_low", "interval_high", "functionality_ID", "timestamp" }.AsReadOnly();

                var filteredQueryParams = ctx.Request.Query.ToList().Where(filterKvp => !removeTheseParams.Contains(filterKvp.Key));
                var filteredQueryString = QueryString.Create(filteredQueryParams);
                ctx.Request.QueryString = filteredQueryString;

                // Store the original body stream for restoring the response body back its original stream
                Stream originalResponseBody = ctx.Response.Body;

                // Create a new memory stream for reading the response; Response body streams are write-only, therefore memory stream is needed here to read
                await using var memStream = new MemoryStream();
                ctx.Response.Body = memStream;

                ctx.Response.OnStarting(() => {
                    ctx.Response.Headers["interval_low"] = svc.ScopedMetadataIntervalLow.ToString();
                    ctx.Response.Headers["interval_high"] = svc.ScopedMetadataIntervalHigh.ToString();
                    ctx.Response.Headers["functionality_ID"] = svc.ScopedMetadataFunctionalityID;
                    ctx.Response.Headers["timestamp"] = svc.ScopedMetadataTimestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"); // "2023-04-03T16:20:30+00:00" for example
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
            else {
                // This is not an HTTP request that requires change
                await _next.Invoke(ctx);
            }
        }
    }

    public static class TCCMiddlewareExtension {
        public static IApplicationBuilder UseTCCMiddleware(this IApplicationBuilder app) {
            return app.UseMiddleware<TCCMiddleware>(); 
        }
    }
}
