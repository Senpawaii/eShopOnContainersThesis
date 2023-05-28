using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.eShopOnContainers.Services.Discount.API.Services;
using Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;
using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure;
using Microsoft.eShopOnContainers.Services.Discount.API.Model;
using System.Collections.Concurrent;
using System.Globalization;
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
        public async Task Invoke(HttpContext ctx, IScopedMetadata svc, ICoordinatorService coordinatorSvc, ISingletonWrapper wrapperSvc, IScopedMetadata scpMetadata, DiscountContext discountContext) {
            Console.WriteLine("Request:" + ctx.Request.Query);
            // To differentiate from a regular call, check for the functionality ID
            if (ctx.Request.Query.TryGetValue("functionality_ID", out var functionality_ID)) {
                svc.ScopedMetadataFunctionalityID = functionality_ID;

                string currentUri = ctx.Request.GetUri().ToString();
                if (currentUri.Contains("commit")) {
                    // Start flushing the Wrapper Data into the Database associated with the functionality
                    ctx.Request.Query.TryGetValue("timestamp", out var ticksStr);
                    long ticks = Convert.ToInt64(ticksStr);

                    //await Task.Delay(30000);

                    wrapperSvc.SingletonWrappedDiscountItems.TryGetValue(functionality_ID, out ConcurrentBag<object[]> objects_to_remove);

                    //_logger.LogInformation($"Committing {objects_to_remove.Count} items for functionality {functionality_ID}.");

                    // Flush the Wrapper Data into the Database
                    FlushWrapper(functionality_ID, wrapperSvc, ticks, scpMetadata, discountContext);

                    DateTime proposedTS = new DateTime(ticks);

                    // Remove the wrapped items from the proposed set
                    wrapperSvc.SingletonRemoveWrappedItemsFromProposedSet(functionality_ID, objects_to_remove);
                    // Remove the functionality from the proposed state
                    wrapperSvc.SingletonRemoveProposedFunctionality(functionality_ID);

                    //_logger.LogInformation($"Cleared {objects_to_remove.Count} items for functionality {functionality_ID}.");

                    await _next.Invoke(ctx);
                    return;
                }
                else if (currentUri.Contains("proposeTS")) {
                    // Update functionality to Proposed State and Store data written in the current functionality in a proposed-state structure
                    var currentTS = scpMetadata.ScopedMetadataTimestamp.Ticks;
                    wrapperSvc.SingletonAddProposedFunctionality(functionality_ID, currentTS);
                    wrapperSvc.SingletonAddWrappedItemsToProposedSet(functionality_ID, currentTS);
                }

                // Check for the other parameters and remove them as needed
                if (ctx.Request.Query.TryGetValue("interval_low", out var interval_lowStr) &&
                    ctx.Request.Query.TryGetValue("interval_high", out var interval_highStr)) {

                    // _logger.LogInformation($"Registered interval: {interval_lowStr}:{interval_highStr}");

                    if (int.TryParse(interval_lowStr, out var interval_low)) {
                        svc.ScopedMetadataIntervalLow = interval_low;
                    }
                    else {
                        // _logger.LogInformation("Failed to parse Low Interval.");
                    }
                    if (int.TryParse(interval_highStr, out var interval_high)) {
                        svc.ScopedMetadataIntervalHigh = interval_high;
                    }
                    else {
                        // _logger.LogInformation("Failed to parse High Interval.");
                    }
                }

                if (ctx.Request.Query.TryGetValue("timestamp", out var timestamp)) {
                    // _logger.LogInformation($"Registered timestamp: {timestamp}");
                    svc.ScopedMetadataTimestamp = DateTime.ParseExact(timestamp, "yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
                }

                if (ctx.Request.Query.TryGetValue("tokens", out var tokens)) {
                    // _logger.LogInformation($"Registered tokens: {tokens}");
                    Double.TryParse(tokens, out double numTokens);
                    svc.ScopedMetadataTokens = numTokens;
                }

                var removeTheseParams = new List<string> { "interval_low", "interval_high", "functionality_ID", "timestamp", "tokens" }.AsReadOnly();

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
                    ctx.Response.Headers["tokens"] = svc.ScopedMetadataTokens.ToString();
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

                if (svc.ScopedMetadataTokens > 0) {
                    // Propose Timestamp with Tokens to the Coordinator
                    await coordinatorSvc.SendTokens();
                }
            }
            else {
                // This is not an HTTP request that requires change
                await _next.Invoke(ctx);
            }
        }

        private void FlushWrapper(string funcID, ISingletonWrapper wrapperSvc, long ticks, IScopedMetadata scpMetadata, DiscountContext discountContext) {
            // Set functionality state to the in commit
            wrapperSvc.SingletonSetTransactionState(funcID, true);

            // Assign the received commit timestamp to the request scope
            scpMetadata.ScopedMetadataTimestamp = new DateTime(ticks);

            // Get stored objects
            var discountWrapperItems = wrapperSvc.SingletonGetDiscountItems(funcID);

            if (discountWrapperItems != null && discountWrapperItems.Count > 0) {
                foreach (object[] item in discountWrapperItems) {
                    DiscountItem newItem = new DiscountItem {
                        //Id = Convert.ToInt32(item[0]),
                        ItemName = Convert.ToString(item[1]),
                        ItemBrand = Convert.ToString(item[2]),
                        ItemType = Convert.ToString(item[3]),
                        DiscountValue = Convert.ToInt32(item[4]),
                    };
                    discountContext.Discount.Add(newItem);
                }
                try {
                    discountContext.SaveChanges();
                } catch (Exception exc) {
                    Console.WriteLine(exc.ToString());
                }
            }

            // Clear the stored objects in the wrapper with this functionality ID
            wrapperSvc.SingletonRemoveFunctionalityObjects(funcID);
        }
    }

    public static class TCCMiddlewareExtension {
        public static IApplicationBuilder UseTCCMiddleware(this IApplicationBuilder app) {
            return app.UseMiddleware<TCCMiddleware>(); 
        }
    }
}
