using Catalog.API.DependencyServices;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.eShopOnContainers.Services.Catalog.API.Infrastructure;
using Microsoft.eShopOnContainers.Services.Catalog.API.Services;
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
        public async Task Invoke(HttpContext ctx, IScopedMetadata svc, ICoordinatorService coordinatorSvc, ISingletonWrapper wrapperSvc, IScopedMetadata scpMetadata, CatalogContext catalogContext) {

            // To differentiate from a regular call, check for the functionality ID
            if (ctx.Request.Query.TryGetValue("functionality_ID", out var functionality_ID)) {
                svc.ScopedMetadataFunctionalityID = functionality_ID;

                string currentUri = ctx.Request.GetUri().ToString();
                if (currentUri.Contains("commit")) {
                    // Start flushing the Wrapper Data into the Database associated with the functionality
                    ctx.Request.Query.TryGetValue("timestamp", out var ticksStr);
                    long ticks = Convert.ToInt64(ticksStr);
                    await FlushWrapper(functionality_ID, wrapperSvc, ticks, scpMetadata, catalogContext);
                    await _next.Invoke(ctx);
                    return;
                }

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

                if (ctx.Request.Query.TryGetValue("timestamp", out var timestamp)) {
                    _logger.LogInformation($"Registered timestamp: {timestamp}");
                    svc.ScopedMetadataTimestamp = DateTime.ParseExact(timestamp, "yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
                }

                if (ctx.Request.Query.TryGetValue("tokens", out var tokens)) {
                    _logger.LogInformation($"Registered tokens: {tokens}");
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

                if(svc.ScopedMetadataTokens > 0) {
                    // Propose Timestamp with Tokens to the Coordinator
                    await coordinatorSvc.ProposeTS();
                }
            }
            else {
                // This is not an HTTP request that requires change
                await _next.Invoke(ctx);
            }
        }

        private Task FlushWrapper(string funcID, ISingletonWrapper wrapperSvc, long ticks, IScopedMetadata scpMetadata, CatalogContext catalogContext) {
            return Task.Factory.StartNew(async () => {
                // Set functionality state to the in commit
                wrapperSvc.SingletonSetTransactionState(funcID, true);
                
                // Assign the received commit timestamp to the request scope
                scpMetadata.ScopedMetadataTimestamp = new DateTime(ticks);

                // Get stored objects
                var catalogWrapperItems = wrapperSvc.SingletonGetCatalogITems(funcID);
                var catalogWrapperBrands = wrapperSvc.SingletonGetCatalogBrands(funcID);
                var catalogWrapperTypes = wrapperSvc.SingletonGetCatalogTypes(funcID);

                if(catalogWrapperItems != null && catalogWrapperItems.Count > 0) {
                    foreach (object[] item in catalogWrapperItems) {
                        CatalogItem newItem = new CatalogItem {
                            Id = Convert.ToInt32(item[0]),
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
                if (catalogWrapperBrands != null && catalogWrapperBrands.Count > 0) {
                    foreach (object[] brand in catalogWrapperBrands) {
                        CatalogBrand newBrand = new CatalogBrand {
                            Id = Convert.ToInt32(brand[0]),
                            Brand = Convert.ToString(brand[1]),
                        };
                        catalogContext.CatalogBrands.Add(newBrand);
                    }
                    await catalogContext.SaveChangesAsync();
                }

                if (catalogWrapperTypes != null && catalogWrapperTypes.Count > 0) {
                    foreach (object[] type in catalogWrapperTypes) {
                        CatalogType newType = new CatalogType {

                            Id = Convert.ToInt32(type[0]),
                            Type = Convert.ToString(type[1]),
                        };
                        catalogContext.CatalogTypes.Add(newType);
                    }
                    await catalogContext.SaveChangesAsync();
                }
            });
        }
    }

    public static class TCCMiddlewareExtension {
        public static IApplicationBuilder UseTCCMiddleware(this IApplicationBuilder app) {
            return app.UseMiddleware<TCCMiddleware>(); 
        }
    }
}
