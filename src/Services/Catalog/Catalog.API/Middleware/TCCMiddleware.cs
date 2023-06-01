﻿using Catalog.API.DependencyServices;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.eShopOnContainers.Services.Catalog.API.DependencyServices;
using Microsoft.eShopOnContainers.Services.Catalog.API.Services;
using System.Collections.Concurrent;

namespace Microsoft.eShopOnContainers.Services.Catalog.API.Middleware {
    public class TCCMiddleware {
        private readonly ILogger<TCCMiddleware> _logger;
        private readonly RequestDelegate _next;

        public TCCMiddleware(ILogger<TCCMiddleware> logger, RequestDelegate next) {
            _logger = logger;
            _next = next;
        }

        // Middleware has access to Scoped Data, dependency-injected at Startup
        public async Task Invoke(HttpContext ctx, IScopedMetadata request_metadata, ICoordinatorService coordinatorSvc, ISingletonWrapper wrapperSvc, CatalogContext catalogContext) {
            // To differentiate from a regular call, check for the clientID
            if (ctx.Request.Query.TryGetValue("clientID", out var clientID)) {
                request_metadata.ClientID = clientID;

                // Initially set the read-only flag to true. Update it as write operations are performed.
                request_metadata.ReadOnly = true;

                string currentUri = ctx.Request.GetUri().ToString();

                if (currentUri.Contains("commit")) {
                    // Start flushing the Wrapper Data into the Database associated with the client session
                    ctx.Request.Query.TryGetValue("timestamp", out var ticksStr);
                    long ticks = Convert.ToInt64(ticksStr);

                    wrapperSvc.SingletonWrappedCatalogItems.TryGetValue(clientID, out ConcurrentBag<object[]> catalog_objects_to_remove);
                    wrapperSvc.SingletonWrappedCatalogBrands.TryGetValue(clientID,out ConcurrentBag<object[]> catalog_brands_to_remove);
                    wrapperSvc.SingletonWrappedCatalogTypes.TryGetValue(clientID, out ConcurrentBag<object[]> catalog_types_to_remove);

                    // Flush the Wrapper Data into the Database
                    await FlushWrapper(clientID, wrapperSvc, ticks, request_metadata, catalogContext);

                    DateTime proposedTS = new DateTime(ticks);

                    // Remove the wrapped items from the proposed set
                    if (catalog_objects_to_remove != null) { 
                        wrapperSvc.SingletonRemoveWrappedItemsFromProposedSet(clientID, catalog_objects_to_remove, "Catalog");
                    }
                    if (catalog_brands_to_remove != null) {
                        wrapperSvc.SingletonRemoveWrappedItemsFromProposedSet(clientID, catalog_brands_to_remove, "CatalogBrand");
                    }
                    if (catalog_types_to_remove != null) {
                        wrapperSvc.SingletonRemoveWrappedItemsFromProposedSet(clientID, catalog_types_to_remove, "CatalogType");
                    }

                    // Remove the client session from the proposed state
                    wrapperSvc.SingletonRemoveProposedFunctionality(clientID);

                    await _next.Invoke(ctx);
                    return;
                } 
                else if(currentUri.Contains("proposeTS")) {
                    // Update client session to Proposed State and Store data written in the current session in a proposed-state structure
                    var currentTS = request_metadata.Timestamp.Ticks;
                    wrapperSvc.SingletonAddProposedFunctionality(clientID, currentTS);
                    wrapperSvc.SingletonAddWrappedItemsToProposedSet(clientID, currentTS);
                }

                if (ctx.Request.Query.TryGetValue("timestamp", out var timestamp)) {
                    //_logger.LogInformation($"Registered timestamp: {timestamp}");
                    request_metadata.Timestamp = DateTime.ParseExact(timestamp, "yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
                }

                //_logger.LogInformation($"Checkpoint 1_d: {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");


                if (ctx.Request.Query.TryGetValue("tokens", out var tokens)) {
                    //_logger.LogInformation($"Registered tokens: {tokens}");
                    Double.TryParse(tokens, out double numTokens);
                    request_metadata.Tokens = numTokens;
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
                    ctx.Response.Headers["clientID"] = request_metadata.ClientID;
                    ctx.Response.Headers["timestamp"] = request_metadata.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"); // "2023-04-03T16:20:30+00:00" for example
                    ctx.Response.Headers["tokens"] = request_metadata.Tokens.ToString();
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

                if(request_metadata.Tokens > 0) {                    
                    // Propose Timestamp with Tokens to the Coordinator
                    await coordinatorSvc.SendTokens();
                }
            }
            else {
                // This is not an HTTP request that requires change
                await _next.Invoke(ctx);
            }
        }

        private async Task FlushWrapper(string clientID, ISingletonWrapper wrapperSvc, long ticks, IScopedMetadata request_metadata, CatalogContext catalogContext) {
            // Set client state to the in commit
            wrapperSvc.SingletonSetTransactionState(clientID, true);
                
            // Assign the received commit timestamp to the request scope
            request_metadata.Timestamp = new DateTime(ticks);

            // Get stored objects
            var catalogWrapperItems = wrapperSvc.SingletonGetCatalogITems(clientID);
            var catalogWrapperBrands = wrapperSvc.SingletonGetCatalogBrands(clientID);
            var catalogWrapperTypes = wrapperSvc.SingletonGetCatalogTypes(clientID);

            if (catalogWrapperBrands != null && catalogWrapperBrands.Count > 0) {
                foreach (object[] brand in catalogWrapperBrands) {
                    CatalogBrand newBrand = new CatalogBrand {
                        //Id = Convert.ToInt32(brand[0]),
                        Brand = Convert.ToString(brand[1]),
                    };
                    catalogContext.CatalogBrands.Add(newBrand);
                }
                await catalogContext.SaveChangesAsync();
            }

            if (catalogWrapperTypes != null && catalogWrapperTypes.Count > 0) {
                foreach (object[] type in catalogWrapperTypes) {
                    CatalogType newType = new CatalogType {

                        //Id = Convert.ToInt32(type[0]),
                        Type = Convert.ToString(type[1]),
                    };
                    catalogContext.CatalogTypes.Add(newType);
                }
                await catalogContext.SaveChangesAsync();
            }
            if (catalogWrapperItems != null && catalogWrapperItems.Count > 0) {
                foreach (object[] item in catalogWrapperItems) {
                    CatalogItem newItem = new CatalogItem {
                        //Id = Convert.ToInt32(item[0]),
                        CatalogBrandId = Convert.ToInt32(item[1]),
                        CatalogTypeId = Convert.ToInt32(item[2]),
                        Description = Convert.ToString(item[3]),
                        Name = Convert.ToString(item[4]),
                        PictureFileName = Convert.ToString(item[5]),
                        Price = Convert.ToDecimal(item[6]),
                        AvailableStock = Convert.ToInt32(item[7]),
                        MaxStockThreshold = Convert.ToInt32(item[8]),
                        OnReorder = Convert.ToBoolean(item[9]),
                        RestockThreshold = Convert.ToInt32(item[10]),
                    };
                    catalogContext.CatalogItems.Add(newItem);
                }
                try {
                    catalogContext.SaveChanges();
                } catch (Exception exc) {
                    Console.WriteLine(exc.ToString());
                }
            }

            // Clear the stored objects in the wrapper with this clientID
            wrapperSvc.SingletonRemoveFunctionalityObjects(clientID);
        }
    
    }

    public static class TCCMiddlewareExtension {
        public static IApplicationBuilder UseTCCMiddleware(this IApplicationBuilder app) {
            return app.UseMiddleware<TCCMiddleware>(); 
        }
    }
}
