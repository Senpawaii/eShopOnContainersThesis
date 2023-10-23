using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.eShopOnContainers.Services.Discount.API.Services;
using Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;
using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure;
using Microsoft.eShopOnContainers.Services.Discount.API.Model;
using System.Collections.Concurrent;
using System.Globalization;
using static System.Net.Mime.MediaTypeNames;
using Microsoft.EntityFrameworkCore;
//using NewRelic.Api.Agent;

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
        //[Trace]
        public async Task Invoke(HttpContext ctx, ICoordinatorService _coordinatorSvc, IScopedMetadata _request_metadata,
            ITokensContextSingleton _remainingTokens, ISingletonWrapper _dataWrapper, IOptions<DiscountSettings> settings) {

            // To differentiate from a regular call, check for the functionality ID
            if (ctx.Request.Query.TryGetValue("clientID", out var clientID)) {
                _request_metadata.ClientID = clientID;

                // Initially set the read-only flag to true. Update it as write operations are performed.
                _request_metadata.ReadOnly = true;

                // Log the current Time and the client ID
                // _logger.LogInformation($"0D: Request received at {DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt", CultureInfo.InvariantCulture)} for functionality {clientID}.");

                string currentUri = ctx.Request.GetUri().ToString();
                if (currentUri.Contains("commit")) {
                    _logger.LogInformation($"ClientID: {clientID} - Committing Transaction at {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");
                    // Start flushing the Wrapper Data into the Database associated with the functionality
                    ctx.Request.Query.TryGetValue("timestamp", out var ticksStr);
                    long ticks = Convert.ToInt64(ticksStr);

                    // Flush the Wrapper Data into the Database
                    await FlushWrapper(clientID, ticks, _dataWrapper, _request_metadata, settings);

                    await _next.Invoke(ctx);
                    // _logger.LogInformation($"ClientID: {clientID} - Transaction Complete at {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");

                    return;
                }
                else if (currentUri.Contains("proposeTS")) {
                    // Update functionality to Proposed State and Store data written in the current functionality in a proposed-state structure
                    var currentTS = _request_metadata.Timestamp.Ticks;
                    // _logger.LogInformation($"ClientID: {clientID} - Proposing Transaction at {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");
                    _dataWrapper.SingletonAddProposedFunctionality(clientID, currentTS);
                    _dataWrapper.SingletonAddWrappedItemsToProposedSet(clientID, currentTS);
                }
                else {
                    // _logger.LogInformation($"ClientID: {clientID} - Starting transaction at {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");
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
                // Log the current Time and the client ID
                // _logger.LogInformation($"0.3D: Request received at {DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt", CultureInfo.InvariantCulture)} for functionality {clientID}.");

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


                // Start a fire and forget task to send the tokens to the coordinator
                Task _ = Task.Run(async () => {
                    if (_remainingTokens.GetRemainingTokens(clientID) > 0) {
                        // _logger.LogInformation($"ClientID: {clientID} - Remaining Tokens: {_remainingTokens.GetRemainingTokens(_request_metadata.ClientID)}");
                        // Propose Timestamp with Tokens to the Coordinator
                        await _coordinatorSvc.SendTokens();
                    }
                    // Clean the singleton fields for the current session context
                    _remainingTokens.RemoveRemainingTokens(clientID);
                });
            }
            else {
                // This is not an HTTP request that requires change
                await _next.Invoke(ctx);
            }
        }

        //[Trace]
        private async Task FlushWrapper(string clientID, long ticks, ISingletonWrapper _data_wrapper, IScopedMetadata _request_metadata, IOptions<DiscountSettings> settings) {
            // _logger.LogInformation($"ClientID: {clientID} - Flushing Wrapper Data to Database");
            // Set functionality state to the in commit
            _logger.LogInformation($"ClientID: {clientID} - Setting Transaction State to true");
            _data_wrapper.SingletonSetTransactionState(clientID, true);

            // Assign the received commit timestamp to the request scope
            _request_metadata.Timestamp = new DateTime(ticks);

            var onlyUpdate = settings.Value.Limit1Version ? true : false;

            // Get stored objects
            var discountWrapperItems = _data_wrapper.SingletonGetWrappedDiscountItemsToFlush(clientID, onlyUpdate);

            using (var scope = _scopeFactory.CreateScope()) {
                var dbContext = scope.ServiceProvider.GetRequiredService<DiscountContext>();
                dbContext.Database.SetCommandTimeout(TimeSpan.FromSeconds(180));

                // Copy the request metadata to the scoped metadata
                var scopedMetadata = scope.ServiceProvider.GetRequiredService<IScopedMetadata>();
                scopedMetadata.ClientID = _request_metadata.ClientID;
                scopedMetadata.Timestamp = _request_metadata.Timestamp;
                scopedMetadata.Tokens = _request_metadata.Tokens;

                if (discountWrapperItems != null) {
                    if (onlyUpdate) {
                        foreach (var discountItem in discountWrapperItems) {
                            dbContext.Discount.Update(discountItem);
                        }
                    }
                    else {
                        foreach (var discountItem in discountWrapperItems) {
                            // _logger.LogInformation($"ClientID: {clientID}, Adding discount item id: {discountItem.Id}, name: {discountItem.ItemName}, brand: {discountItem.ItemBrand}, type: {discountItem.ItemType}, discount: {discountItem.DiscountValue}");

                            // Make a copy of the discountItem with the Id = 0 (EF Core will generate a new Id for the new item, which is the PK)
                            var discountItemCopy = new DiscountItem() {
                                ItemName = discountItem.ItemName,
                                ItemBrand = discountItem.ItemBrand,
                                ItemType = discountItem.ItemType,
                                DiscountValue = discountItem.DiscountValue
                            };
                            dbContext.Discount.Add(discountItemCopy);
                        }
                    }
                    await dbContext.SaveChangesAsync();
                }
            }
            // _logger.LogInformation($"ClientID: {clientID} - Wrapper Data flushed to Database at {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");    
            // The items have been committed. Notify all threads waiting on the commit to read
            _data_wrapper.NotifyReaderThreads(clientID, discountWrapperItems);
            // There are 3 data types that need to be cleaned: Wrapped items, Functionality State, and Proposed Objects
            _data_wrapper.CleanWrappedObjects(clientID);
            // _logger.LogInformation($"ClientID: {clientID} - Wrapper Data flushed to Database");    
        }
    }

    public static class TCCMiddlewareExtension {
        public static IApplicationBuilder UseTCCMiddleware(this IApplicationBuilder app) {
            return app.UseMiddleware<TCCMiddleware>();
        }
    }
}
