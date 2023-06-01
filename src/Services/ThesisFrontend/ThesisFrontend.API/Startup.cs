using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.DependencyServices;
using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Infrastructure.Filters;
using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Infrastructure.HttpHandlers;
using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Middleware;
using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Services;

namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API;
public class Startup {
    public Startup(IConfiguration configuration) {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public IServiceProvider ConfigureServices(IServiceCollection services) {
        services
            .AddAppInsight(Configuration)
            .AddCustomMVC(Configuration)
            .AddHttpContextAccessor()
            .AddEntityFrameworkSqlServer()
            .AddCustomOptions(Configuration)
            .AddSwagger(Configuration)
            .AddCustomHealthCheck(Configuration);

        if (Configuration["ThesisWrapperEnabled"] == "True") {
            services.AddScoped<TCCHttpInjector>();
            services.AddScoped<IScopedMetadata, ScopedMetadata>();
            //    .AddSingleton<ISingletonWrapper, SingletonWrapper>();
            services.AddHttpClient<ICatalogService, CatalogService>().AddHttpMessageHandler<TCCHttpInjector>();
            services.AddHttpClient<IDiscountService, DiscountService>().AddHttpMessageHandler<TCCHttpInjector>();
            services.AddHttpClient<IBasketService, BasketService>().AddHttpMessageHandler<TCCHttpInjector>();
        } else {
            // Register the HTTP clients without the TCCHttpInjector
            services.AddHttpClient<ICatalogService, CatalogService>();
            services.AddHttpClient<IDiscountService, DiscountService>();
            services.AddHttpClient<IBasketService, BasketService>();
        }

        var container = new ContainerBuilder();
        container.Populate(services);

        return new AutofacServiceProvider(container.Build());
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory) {
        //Configure logs

        //loggerFactory.AddAzureWebAppDiagnostics();
        //loggerFactory.AddApplicationInsights(app.ApplicationServices, LogLevel.Trace);

        var pathBase = Configuration["PATH_BASE"];

        if (!string.IsNullOrEmpty(pathBase)) {
            loggerFactory.CreateLogger<Startup>().LogDebug("Using PATH BASE '{pathBase}'", pathBase);
            app.UsePathBase(pathBase);
        }

        app.UseSwagger()
            .UseSwaggerUI(c => {
                c.SwaggerEndpoint($"{(!string.IsNullOrEmpty(pathBase) ? pathBase : string.Empty)}/swagger/v1/swagger.json", "ThesisFrontend.API V1");
            });

        app.UseRouting();
        app.UseCors("CorsPolicy");

        bool wrapperEnabled = Convert.ToBoolean(Configuration["ThesisWrapperEnabled"]);
        if (wrapperEnabled) {
            app.UseTCCMiddleware();
        }

        app.UseEndpoints(endpoints => {
            endpoints.MapDefaultControllerRoute();

            endpoints.MapControllers();

            endpoints.MapHealthChecks("/hc", new HealthCheckOptions() {
                Predicate = _ => true,
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            endpoints.MapHealthChecks("/liveness", new HealthCheckOptions {
                Predicate = r => r.Name.Contains("self")
            });
        });
    }
}


public static class CustomExtensionMethods {
    public static IServiceCollection AddAppInsight(this IServiceCollection services, IConfiguration configuration) {
        services.AddApplicationInsightsTelemetry(configuration);
        services.AddApplicationInsightsKubernetesEnricher();

        return services;
    }

    public static IServiceCollection AddCustomMVC(this IServiceCollection services, IConfiguration configuration) {
        services.AddControllers(options => {
            options.Filters.Add(typeof(HttpGlobalExceptionFilter));
        })
        .AddJsonOptions(options => options.JsonSerializerOptions.WriteIndented = true);

        services.AddCors(options => {
            options.AddPolicy("CorsPolicy",
                builder => builder
                .SetIsOriginAllowed((host) => true)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials());
        });

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

        return services;
    }

    public static IServiceCollection AddCustomOptions(this IServiceCollection services, IConfiguration configuration) {
        services.Configure<ThesisFrontendSettings>(configuration);
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

    public static IServiceCollection AddSwagger(this IServiceCollection services, IConfiguration configuration) {
        services.AddSwaggerGen(options => {
            options.SwaggerDoc("v1", new OpenApiInfo {
                Title = "eShopOnContainers - ThesisFrontend HTTP API",
                Version = "v1",
                Description = "The ThesisFrontend Microservice HTTP API. This is a Data-Driven/CRUD microservice sample"
            });
        });

        return services;

    }
}
