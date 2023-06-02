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
        private readonly IServiceScopeFactory _scopeFactory;


        public TCCMiddleware(ILogger<TCCMiddleware> logger, RequestDelegate next, IServiceScopeFactory scopeFactory) {
            _logger = logger;
            _next = next;
            _scopeFactory = scopeFactory;
        }

        // Middleware has access to Scoped Data, dependency-injected at Startup
        public async Task Invoke(HttpContext ctx, ICoordinatorService _coordinatorSvc, IScopedMetadata _request_metadata, 
            ITokensContextSingleton _remainingTokens, ISingletonWrapper _data_wrapper) {
            
            // Console.WriteLine("Request:" + ctx.Request.Query);
            // To differentiate from a regular call, check for the functionality ID
            if (ctx.Request.Query.TryGetValue("clientID", out var clientID)) {
                _request_metadata.ClientID = clientID;

                // Initially set the read-only flag to true. Update it as write operations are performed.
                _request_metadata.ReadOnly = true;

                string currentUri = ctx.Request.GetUri().ToString();
                if (currentUri.Contains("commit")) {
                    // Start flushing the Wrapper Data into the Database associated with the functionality
                    ctx.Request.Query.TryGetValue("timestamp", out var ticksStr);
                    long ticks = Convert.ToInt64(ticksStr);

                    //await Task.Delay(30000);

                    _data_wrapper.Singleton_Wrapped_DiscountItems.TryGetValue(clientID, out ConcurrentBag<object[]> objects_to_remove);

                    //_logger.LogInformation($"Committing {objects_to_remove.Count} items for functionality {clientID}.");

                    // Flush the Wrapper Data into the Database
                    FlushWrapper(clientID, ticks, _data_wrapper, _request_metadata);

                    DateTime proposedTS = new DateTime(ticks);

                    // Remove the wrapped items from the proposed set
                    _data_wrapper.SingletonRemoveWrappedItemsFromProposedSet(clientID, objects_to_remove);
                    // Remove the functionality from the proposed state
                    _data_wrapper.SingletonRemoveProposedFunctionality(clientID);

                    //_logger.LogInformation($"Cleared {objects_to_remove.Count} items for functionality {clientID}.");

                    await _next.Invoke(ctx);
                    return;
                }
                else if (currentUri.Contains("proposeTS")) {
                    // Update functionality to Proposed State and Store data written in the current functionality in a proposed-state structure
                    var currentTS = _request_metadata.Timestamp.Ticks;
                    _data_wrapper.SingletonAddProposedFunctionality(clientID, currentTS);
                    _data_wrapper.SingletonAddWrappedItemsToProposedSet(clientID, currentTS);
                }

                if (ctx.Request.Query.TryGetValue("timestamp", out var timestamp)) {
                    // _logger.LogInformation($"Registered timestamp: {timestamp}");
                    _request_metadata.Timestamp = DateTime.ParseExact(timestamp, "yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
                }

                if (ctx.Request.Query.TryGetValue("tokens", out var tokens)) {
                    // _logger.LogInformation($"Registered tokens: {tokens}");
                    Int32.TryParse(tokens, out int numTokens);
                    _request_metadata.Tokens = numTokens;
                    _remainingTokens.AddRemainingTokens(clientID, numTokens);
                }

                var removeTheseParams = new List<string> { "clientID", "timestamp", "tokens" }.AsReadOnly();

                var filteredQueryParams = ctx.Request.Query.ToList().Where(filterKvp => !removeTheseParams.Contains(filterKvp.Key));
                var filteredQueryString = QueryString.Create(filteredQueryParams);
                ctx.Request.QueryString = filteredQueryString;

                // Store the original body stream for restoring the response body back its original stream
                Stream originalResponseBody = ctx.Response.Body;

                // Create a new memory stream for reading the response; Response body streams are write-only, therefore memory stream is needed here to read
                await using var memStream = new MemoryStream();
                ctx.Response.Body = memStream;

                ctx.Response.OnStarting(() => {
                    ctx.Response.Headers["clientID"] = _request_metadata.ClientID;
                    ctx.Response.Headers["timestamp"] = _request_metadata.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"); // "2023-04-03T16:20:30+00:00" for example
                    ctx.Response.Headers["tokens"] = _request_metadata.Tokens.ToString();
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

                if (_remainingTokens.GetRemainingTokens(_request_metadata.ClientID) > 0) {
                    // Propose Timestamp with Tokens to the Coordinator
                    await _coordinatorSvc.SendTokens();
                }
                _remainingTokens.RemoveRemainingTokens(_request_metadata.ClientID);
            }
            else {
                // This is not an HTTP request that requires change
                await _next.Invoke(ctx);
            }
        }

        private void FlushWrapper(string clientID, long ticks, ISingletonWrapper _data_wrapper, IScopedMetadata _request_metadata) {
            
            // Set functionality state to the in commit
            _data_wrapper.SingletonSetTransactionState(clientID, true);

            // Assign the received commit timestamp to the request scope
            _request_metadata.Timestamp = new DateTime(ticks);

            // Get stored objects
            var discountWrapperItems = _data_wrapper.SingletonGetDiscountItems(clientID);

            using (var scope = _scopeFactory.CreateScope()) {
                var dbContext = scope.ServiceProvider.GetRequiredService<DiscountContext>();
                
                // Copy the request metadata to the scoped metadata
                var scopedMetadata = scope.ServiceProvider.GetRequiredService<IScopedMetadata>();
                scopedMetadata.ClientID = _request_metadata.ClientID;
                scopedMetadata.Timestamp = _request_metadata.Timestamp;
                scopedMetadata.Tokens = _request_metadata.Tokens;
                
                if (discountWrapperItems != null && discountWrapperItems.Count > 0) {
                    foreach (object[] item in discountWrapperItems) {
                        DiscountItem newItem = new DiscountItem {
                            //Id = Convert.ToInt32(item[0]),
                            ItemName = Convert.ToString(item[1]),
                            ItemBrand = Convert.ToString(item[2]),
                            ItemType = Convert.ToString(item[3]),
                            DiscountValue = Convert.ToInt32(item[4]),
                        };
                        dbContext.Discount.Add(newItem);
                    }
                    try {
                        dbContext.SaveChanges();
                    } catch (Exception exc) {
                        Console.WriteLine(exc.ToString());
                    }
                }
            }

            // Clear the stored objects in the wrapper with this functionality ID
            _data_wrapper.SingletonRemoveFunctionalityObjects(clientID);
        }
    }

    public static class TCCMiddlewareExtension {
        public static IApplicationBuilder UseTCCMiddleware(this IApplicationBuilder app) {
            return app.UseMiddleware<TCCMiddleware>(); 
        }
    }
}
