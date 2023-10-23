using Microsoft.eShopOnContainers.Services.Basket.API.DependencyServices;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.eShopOnContainers.Services.Basket.API.Infrastructure;
using Microsoft.eShopOnContainers.Services.Basket.API.Services;
using System.Collections.Concurrent;
using System.Globalization;
using static System.Net.Mime.MediaTypeNames;
using Basket.API.DependencyServices;

namespace Microsoft.eShopOnContainers.Services.Basket.API.Middleware;
public class TCCMiddleware {
    private readonly ILogger<TCCMiddleware> _logger;
    private readonly RequestDelegate _next;
    private IScopedMetadata _request_metadata;
    private ITokensContextSingleton _remainingTokens;
    private readonly ICoordinatorService _coordinatorSvc;

    public TCCMiddleware(ILogger<TCCMiddleware> logger, RequestDelegate next, ICoordinatorService coordinatorSvc, IScopedMetadata metadata, ITokensContextSingleton remainingTokens) {
        _logger = logger;
        _next = next;
        _request_metadata = metadata;
        _remainingTokens = remainingTokens;
        _coordinatorSvc = coordinatorSvc;
    }

    // Middleware has access to Scoped Data, dependency-injected at Startup
    public async Task Invoke(HttpContext ctx, ISingletonWrapper _dataWrapper) {

        // To differentiate from a regular call, check for the client ID
        if (ctx.Request.Query.TryGetValue("clientID", out var clientID)) {
            // Store the client ID in the scoped metadata
            _request_metadata.ClientID.Value = clientID;

            string currentUri = ctx.Request.GetUri().ToString();

            if (currentUri.Contains("confirm")) {
                // _logger.LogInformation($"Received a confirmation request from Coordinator for client {clientID}.");
                // The functionality that led to the event is now confirmed
                await _dataWrapper.ConfirmFunctionality(clientID);
            }
            else {
                // Initially set the read-only flag to true. Update it as write operations are performed.
                _request_metadata.ReadOnly.Value = true;
                if (ctx.Request.Query.TryGetValue("timestamp", out var timestamp)) {
                    //_logger.LogInformation($"Registered timestamp: {timestamp}");
                    _request_metadata.Timestamp.Value = DateTime.ParseExact(timestamp, "yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
                }

                if (ctx.Request.Query.TryGetValue("tokens", out var tokens)) {
                    //_logger.LogInformation($"Registered tokens: {tokens}");
                    if (!int.TryParse(tokens, out int numTokens)) {
                        _logger.LogError("Couldn't extract the number of tokens from the request query string.");
                    }
                    _request_metadata.Tokens.Value = numTokens;
                    _remainingTokens.AddRemainingTokens(clientID, numTokens);
                }
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
                ctx.Response.Headers["clientID"] = _request_metadata.ClientID.Value;
                ctx.Response.Headers["timestamp"] = _request_metadata.Timestamp.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"); // "2023-04-03T16:20:30+00:00" for example
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

            // Decrement the tokens
            int remainingTokens = _remainingTokens.GetRemainingTokens(clientID);
            _remainingTokens.DecrementRemainingTokens(clientID, remainingTokens);

            if (remainingTokens > 0) {
                // Start a fire and forget task to send the tokens to the coordinator
                Task _ = Task.Run(_coordinatorSvc.SendTokens);

                // Clean the singleton fields for the current session context
                _remainingTokens.RemoveRemainingTokens(clientID);
            }
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
