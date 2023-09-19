using Microsoft.eShopOnContainers.Services.Coordinator.API.Grpc;
using Microsoft.eShopOnContainers.Services.Coordinator.API.Infrastructure.Filters;
using Microsoft.eShopOnContainers.Services.Coordinator.API.Services;
using Coordinator.API.Services;

namespace Microsoft.eShopOnContainers.Services.Coordinator.API;

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
            .AddCustomOptions(Configuration)
            .AddSwagger(Configuration)
            .AddCustomHealthCheck(Configuration)
            .AddSingleton<IFunctionalityService, FunctionalityService>();

        // Add CatalogClient and DiscountClient dependencies
        services.AddHttpClient<ICatalogService, CatalogService>();
        services.AddHttpClient<IDiscountService, DiscountService>();
        services.AddHttpClient<IThesisFrontendService, ThesisFrontendService>();
        services.AddHttpClient<IBasketService, BasketService>();

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
                c.SwaggerEndpoint($"{(!string.IsNullOrEmpty(pathBase) ? pathBase : string.Empty)}/swagger/v1/swagger.json", "Coordinator.API V1");
            });

        app.UseRouting();
        app.UseCors("CorsPolicy");

        app.UseEndpoints(endpoints => {
            endpoints.MapDefaultControllerRoute();

            endpoints.MapControllers();

            //endpoints.MapHealthChecks("/hc", new HealthCheckOptions() {
            //    Predicate = _ => true,
            //    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            //});

            //endpoints.MapHealthChecks("/liveness", new HealthCheckOptions {
            //    Predicate = r => r.Name.Contains("self")
            //});
        });

        //app.UseRouter(buildRouter(app));
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

        //var hcBuilder = services.AddHealthChecks();

        //hcBuilder
        //    .AddCheck("self", () => HealthCheckResult.Healthy());

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

    public static IServiceCollection AddSwagger(this IServiceCollection services, IConfiguration configuration) {
        services.AddSwaggerGen(options => {
            options.SwaggerDoc("v1", new OpenApiInfo {
                Title = "eShopOnContainers - Coordinator HTTP API",
                Version = "v1",
                Description = "The Coordinator Microservice HTTP API."
            });
        });

        return services;

    }
}
