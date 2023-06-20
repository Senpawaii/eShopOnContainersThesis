using Catalog.API.DependencyServices;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.eShopOnContainers.Services.Catalog.API.DependencyServices;
using Microsoft.eShopOnContainers.Services.Catalog.API.Infrastructure;
using Microsoft.eShopOnContainers.Services.Catalog.API.Services;
using System.Collections.Concurrent;
using NewRelic.Api.Agent;
using System.Diagnostics;

namespace Microsoft.eShopOnContainers.Services.Catalog.API.Middleware {
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
        [Trace]
        public async Task Invoke(HttpContext ctx, ICoordinatorService _coordinatorSvc, IScopedMetadata _request_metadata,
            ITokensContextSingleton _remainingTokens, ISingletonWrapper _dataWrapper, IOptions<CatalogSettings> settings) {

            // To differentiate from a regular call, check for the clientID
            if (ctx.Request.Query.TryGetValue("clientID", out var clientID)) {
                // Start timer
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                _request_metadata.ClientID = clientID;

                // Initially set the read-only flag to true. Update it as write operations are performed.
                _request_metadata.ReadOnly = true;

                string currentUri = ctx.Request.GetUri().ToString();

                if (currentUri.Contains("commit")) {
                    // Start flushing the Wrapper Data into the Database associated with the client session
                    ctx.Request.Query.TryGetValue("timestamp", out var ticksStr);
                    long ticks = Convert.ToInt64(ticksStr);

                    // TODO: instead of getting this data, call a clean method on the wrapper wit the client ID
                    //_dataWrapper.SingletonWrappedCatalogItems.TryGetValue(clientID, out ConcurrentBag<object[]> catalog_objects_to_remove);
                    //_dataWrapper.SingletonWrappedCatalogBrands.TryGetValue(clientID,out ConcurrentBag<object[]> catalog_brands_to_remove);
                    //_dataWrapper.SingletonWrappedCatalogTypes.TryGetValue(clientID, out ConcurrentBag<object[]> catalog_types_to_remove);

                    // Flush the Wrapper Data into the Database
                    await FlushWrapper(clientID, ticks, _dataWrapper, _request_metadata, settings);

                    

                    await _next.Invoke(ctx);
                    
                    // Stop timer
                    stopwatch.Stop();
                    // Log the time taken to commit
                    _logger.LogInformation($"Commit time: {stopwatch.ElapsedMilliseconds} ms");
                    return;
                } 
                else if(currentUri.Contains("proposeTS")) {
                    // Update client session to Proposed State and Store data written in the current session in a proposed-state structure
                    var currentTS = _request_metadata.Timestamp.Ticks;
                    _dataWrapper.SingletonAddProposedFunctionality(clientID, currentTS);
                    _dataWrapper.SingletonAddWrappedItemsToProposedSetV2(clientID, currentTS);
                }

                if (ctx.Request.Query.TryGetValue("timestamp", out var timestamp)) {
                    //_logger.LogInformation($"Registered timestamp: {timestamp}");
                    _request_metadata.Timestamp = DateTime.ParseExact(timestamp, "yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
                }

                //_logger.LogInformation($"Checkpoint 1_d: {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");


                if (ctx.Request.Query.TryGetValue("tokens", out var tokens)) {
                    //_logger.LogInformation($"Registered tokens: {tokens}");
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

                if(_remainingTokens.GetRemainingTokens(_request_metadata.ClientID) > 0) {                    
                    // Propose Timestamp with Tokens to the Coordinator
                    await _coordinatorSvc.SendTokens();
                }
                // Clean the singleton fields for the current session context
                _remainingTokens.RemoveRemainingTokens(_request_metadata.ClientID);

                // Stop timer
                stopwatch.Stop();
                // Log the time taken
                _logger.LogInformation($"Propose time: {stopwatch.ElapsedMilliseconds} ms");
            }
            else {
                // This is not an HTTP request that requires change
                await _next.Invoke(ctx);
            }
        }

        [Trace]
        private async Task FlushWrapper(string clientID, long ticks, ISingletonWrapper _dataWrapper, IScopedMetadata _request_metadata, IOptions<CatalogSettings> settings) {
            // Set client state to the in commit
            _dataWrapper.SingletonSetTransactionState(clientID, true);
                
            // Assign the received commit timestamp to the request scope
            _request_metadata.Timestamp = new DateTime(ticks);

            using (var scope = _scopeFactory.CreateScope()) {
                var dbContext = scope.ServiceProvider.GetRequiredService<CatalogContext>();

                // Copy the request metadata to the scoped metadata
                var scopedMetadata = scope.ServiceProvider.GetRequiredService<IScopedMetadata>();
                scopedMetadata.ClientID = _request_metadata.ClientID;
                scopedMetadata.Timestamp = _request_metadata.Timestamp;
                scopedMetadata.Tokens = _request_metadata.Tokens;

                var onlyUpdate = settings.Value.Limit1Version ? true : false;

                await _dataWrapper.FlushDataToDatabase(clientID, dbContext, onlyUpdate);
            }
            // There are 3 data types that need to be cleaned: Wrapped items, Functionality State, and Proposed Objects
            _dataWrapper.CleanWrappedObjects(clientID);            
            // Clear the stored objects in the wrapper with this clientID
            //_dataWrapper.SingletonRemoveFunctionalityObjects(clientID);

            //// Remove the wrapped items from the proposed set
            //if (catalog_objects_to_remove != null) {
            //    _dataWrapper.SingletonRemoveWrappedItemsFromProposedSetV2(clientID, catalog_objects_to_remove, "Catalog");
            //}
            //if (catalog_brands_to_remove != null) {
            //    _dataWrapper.SingletonRemoveWrappedItemsFromProposedSetV2(clientID, catalog_brands_to_remove, "CatalogBrand");
            //}
            //if (catalog_types_to_remove != null) {
            //    _dataWrapper.SingletonRemoveWrappedItemsFromProposedSetV2(clientID, catalog_types_to_remove, "CatalogType");
            //}

            //// Remove the client session from the proposed state
            //_dataWrapper.SingletonRemoveProposedFunctionality(clientID);

        }

    }

    public static class TCCMiddlewareExtension {
        public static IApplicationBuilder UseTCCMiddleware(this IApplicationBuilder app) {
            return app.UseMiddleware<TCCMiddleware>(); 
        }
    }
}
