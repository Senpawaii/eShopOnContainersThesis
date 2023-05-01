using Autofac.Extensions.DependencyInjection;
using Autofac;
using Microsoft.Extensions.DependencyInjection;
using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RabbitMQ.Client;
using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.eShopOnContainers.Services.Coordinator.API;

public class Startup {
    public Startup(IConfiguration configuration) {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public IServiceProvider ConfigureServices(IServiceCollection services) {
        services
            .AddAppInsight(Configuration)
            .AddGrpc().Services
            .AddHttpContextAccessor()
            .AddCustomOptions(Configuration)
            //.AddSwagger(Configuration)
            .AddCustomHealthCheck(Configuration)
            .AddGrpcServices();

        var container = new ContainerBuilder();
        container.Populate(services);

        return new AutofacServiceProvider(container.Build());
    }
}

public static class CustomExtensionMethods {
    public static IServiceCollection AddAppInsight(this IServiceCollection services, IConfiguration configuration) {
        services.AddApplicationInsightsTelemetry(configuration);
        services.AddApplicationInsightsKubernetesEnricher();

        return services;
    }

    public static IServiceCollection AddCustomHealthCheck(this IServiceCollection services, IConfiguration configuration) {
        var accountName = configuration.GetValue<string>("AzureStorageAccountName");
        var accountKey = configuration.GetValue<string>("AzureStorageAccountKey");

        var hcBuilder = services.AddHealthChecks();

        hcBuilder
            .AddCheck("self", () => HealthCheckResult.Healthy())
            .AddSqlServer(
                configuration["ConnectionString"],
                name: "CatalogDB-check",
                tags: new string[] { "catalogdb" });

        if (!string.IsNullOrEmpty(accountName) && !string.IsNullOrEmpty(accountKey)) {
            hcBuilder
                .AddAzureBlobStorage(
                    $"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey={accountKey};EndpointSuffix=core.windows.net",
                    name: "catalog-storage-check",
                    tags: new string[] { "catalogstorage" });
        }

        if (configuration.GetValue<bool>("AzureServiceBusEnabled")) {
            hcBuilder
                .AddAzureServiceBusTopic(
                    configuration["EventBusConnection"],
                    topicName: "eshop_event_bus",
                    name: "catalog-servicebus-check",
                    tags: new string[] { "servicebus" });
        }
        else {
            hcBuilder
                .AddRabbitMQ(
                    $"amqp://{configuration["EventBusConnection"]}",
                    name: "catalog-rabbitmqbus-check",
                    tags: new string[] { "rabbitmqbus" });
        }

        return services;
    }

    public static IServiceCollection AddCustomOptions(this IServiceCollection services, IConfiguration configuration) {
        services.Configure<CoordinatorSettings>(configuration);
        services.Configure<ApiBehaviorOptions>(options => {
            options.InvalidModelStateResponseFactory = context => {
                var problemDetails = new ValidationProblemDetails(context.ModelState) {
                    Instance = context.HttpContext.Request.Path,
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "Please refer to the errors property for additional details."
                };

                return new BadRequestObjectResult(problemDetails) {
                    ContentTypes = { "application/problem+json", "application/problem+xml" }
                };
            };
        });

        return services;
    }

    //public static IServiceCollection AddSwagger(this IServiceCollection services, IConfiguration configuration) {
    //    services.AddSwaggerGen(options => {
    //        options.SwaggerDoc("v1", new OpenApiInfo {
    //            Title = "eShopOnContainers - Catalog HTTP API",
    //            Version = "v1",
    //            Description = "The Catalog Microservice HTTP API. This is a Data-Driven/CRUD microservice sample"
    //        });
    //    });

    //    return services;

    //}

    public static IServiceCollection AddGrpcServices(this IServiceCollection services) {
        //services.AddTransient<GrpcExceptionInterceptor>();

        //services.AddScoped<IBasketService, BasketService>();

        //services.AddGrpcClient<Basket.BasketClient>((services, options) => {
        //    var basketApi = services.GetRequiredService<IOptions<UrlsConfig>>().Value.GrpcBasket;
        //    options.Address = new Uri(basketApi);
        //}).AddInterceptor<GrpcExceptionInterceptor>();

        services.AddScoped<ICatalogService, CatalogService>();

        services.AddGrpcClient<Catalog.CatalogClient>((services, options) => {
            var catalogApi = services.GetRequiredService<IOptions<UrlsConfig>>().Value.GrpcCatalog;
            options.Address = new Uri(catalogApi);
        });
            //.AddInterceptor<GrpcExceptionInterceptor>();

        //services.AddScoped<IOrderingService, OrderingService>();

        //services.AddGrpcClient<OrderingGrpc.OrderingGrpcClient>((services, options) => {
        //    var orderingApi = services.GetRequiredService<IOptions<UrlsConfig>>().Value.GrpcOrdering;
        //    options.Address = new Uri(orderingApi);
        //}).AddInterceptor<GrpcExceptionInterceptor>();

        return services;
    }
}
