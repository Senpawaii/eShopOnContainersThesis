﻿using Grpc.Core;
using k8s.KubeConfigModels;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.DependencyServices;
using System.Threading;

namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Middleware;
public class TCCMiddleware {
    private readonly ILogger<TCCMiddleware> _logger;
    private readonly RequestDelegate _next;
    private static Random random = new Random();

    public TCCMiddleware(ILogger<TCCMiddleware> logger, RequestDelegate next) {
        _logger = logger;
        _next = next;
    }

    public async Task Invoke(HttpContext httpctx, IScopedMetadata metadata) {
        string invokedPath = httpctx.Request.Path;

        // Initialize the metadata fields
        SeedMetadata(metadata);
        
        switch (invokedPath) {
            case "/api/v1/frontend/readbasket":
                _logger.LogInformation("TCCMiddleware: Invoked Path: {0}", invokedPath);
                await HandleReadBasketFunctionality(httpctx, metadata);
                break;
            default:
                _logger.LogInformation("TCCMiddleware: Invoked Path: {0}", invokedPath);
                await _next.Invoke(httpctx);
                break;
        }
    }
    private void SeedMetadata(IScopedMetadata metadata) {
        // Generate a 32 bit random string for the client ID
        string clientID = GenerateRandomString(32);

        // Generate a timestamp for the request
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");

        // Generate tokens
        int tokens = 1000000000;

        metadata.Tokens.Value = tokens;
        metadata.Timestamp.Value = timestamp;
        metadata.ClientID.Value = clientID;
    }

    private string GenerateRandomString(int length) {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, length)
                       .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    private async Task HandleReadBasketFunctionality(HttpContext httpctx, IScopedMetadata metadata) {
        // Set the number of invocation to the services that are invoked in this graph's path
        metadata.Invocations.Value = 3;
        await _next.Invoke(httpctx);
    }
}

public static class TCCMiddlewareExtension {
    public static IApplicationBuilder UseTCCMiddleware(this IApplicationBuilder app) {
        return app.UseMiddleware<TCCMiddleware>();
    }
}
