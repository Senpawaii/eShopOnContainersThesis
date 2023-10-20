using Microsoft.EntityFrameworkCore;
using Microsoft.eShopOnContainers.BuildingBlocks.EventBus;
using Microsoft.eShopOnContainers.BuildingBlocks.EventBus.Abstractions;
using Microsoft.eShopOnContainers.BuildingBlocks.EventBusRabbitMQ;
using Microsoft.eShopOnContainers.BuildingBlocks.EventBusServiceBus;
using Microsoft.eShopOnContainers.BuildingBlocks.IntegrationEventLogEF;
using Microsoft.eShopOnContainers.BuildingBlocks.IntegrationEventLogEF.Services;
using Microsoft.eShopOnContainers.Services.Discount.API;
using Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;
using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure;
using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure.Filters;
using Microsoft.eShopOnContainers.Services.Discount.API.Middleware;
using Microsoft.eShopOnContainers.Services.Discount.API.Services;
using System.Data.Common;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using System.Data;
using Microsoft.eShopOnContainers.Services.Discount.API.IntegrationEvents;
using Discount.API.IntegrationEvents.Events.Factories;

namespace Discount.API;
public class Startup {
    public Startup(IConfiguration configuration) {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public IServiceProvider ConfigureServices(IServiceCollection services) { 
        services
            .AddAppInsight(Configuration)
            .AddGrpc().Services
            .AddCustomMVC(Configuration)
            .AddHttpContextAccessor()
            .AddEntityFrameworkSqlServer()
            .AddCustomDbContext(Configuration)
            .AddCustomOptions(Configuration)
            .AddIntegrationServices(Configuration)
            .AddEventBus(Configuration)
            .AddSwagger(Configuration)
            .AddCustomHealthCheck(Configuration)
            
            .AddScoped<IScopedMetadata, ScopedMetadata>();


        if (Configuration["ThesisWrapperEnabled"] == "True") {
            services
                .AddSingleton<ITokensContextSingleton, TokensContextSingleton>()
                .AddSingleton<ISingletonWrapper, SingletonWrapper>()
                .AddHttpClient<ICoordinatorService, CoordinatorService>();

            if (Configuration["Limit1Version"] == "False") {
                services.AddHostedService<GarbageCollectionService>();
            }
        }

        var container = new ContainerBuilder();
        container.Populate(services);

        return new AutofacServiceProvider(container.Build());
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory) {
        //Configure logs

        //loggerFactory.AddAzureWebAppDiagnostics();
        loggerFactory.AddApplicationInsights(app.ApplicationServices, LogLevel.Trace);

        var pathBase = Configuration["PATH_BASE"];

        if (!string.IsNullOrEmpty(pathBase)) {
            loggerFactory.CreateLogger<Startup>().LogDebug("Using PATH BASE '{pathBase}'", pathBase);
            app.UsePathBase(pathBase);
        }

        app.UseSwagger()
            .UseSwaggerUI(c => {
                c.SwaggerEndpoint($"{(!string.IsNullOrEmpty(pathBase) ? pathBase : string.Empty)}/swagger/v1/swagger.json", "Discount.API V1");
            });

        app.UseRouting();
        app.UseCors("CorsPolicy");

        // TODO The middleware should only be called for the /api/v1/discount/items (and 2 more routes), instead of all Discount routes. How can we do this? 
        bool wrapperEnabled = Convert.ToBoolean(Configuration["ThesisWrapperEnabled"]);
        Console.WriteLine($"Wrapper Enabled: {wrapperEnabled}");

        if (wrapperEnabled) {
            // Log the the boolean
            Console.WriteLine($"Wrapper Enabled if: {wrapperEnabled}");
            app.UseTCCMiddleware();
        }

        app.UseEndpoints(endpoints => {
            endpoints.MapDefaultControllerRoute();

            endpoints.MapControllers();

            // endpoints.MapGet("/_proto/", async ctx => {
            //     ctx.Response.ContentType = "text/plain";
            //     using var fs = new FileStream(Path.Combine(env.ContentRootPath, "Proto", "discount.proto"), FileMode.Open, FileAccess.Read);
            //     using var sr = new StreamReader(fs);
            //     while (!sr.EndOfStream) {
            //         var line = await sr.ReadLineAsync();
            //         if (line != "/* >>" || line != "<< */") {
            //             await ctx.Response.WriteAsync(line);
            //         }
            //     }
            // });

            // endpoints.MapGrpcService<DiscountService>();

            endpoints.MapHealthChecks("/hc", new HealthCheckOptions() {
                Predicate = _ => true,
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            endpoints.MapHealthChecks("/liveness", new HealthCheckOptions {
                Predicate = r => r.Name.Contains("self")
            });
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

        var hcBuilder = services.AddHealthChecks();

        hcBuilder
            .AddCheck("self", () => HealthCheckResult.Healthy())
            .AddSqlServer(
                configuration["ConnectionString"],
                name: "DiscountDB-check",
                tags: new string[] { "discountdb" });

        if (!string.IsNullOrEmpty(accountName) && !string.IsNullOrEmpty(accountKey)) {
            hcBuilder
                .AddAzureBlobStorage(
                    $"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey={accountKey};EndpointSuffix=core.windows.net",
                    name: "discount-storage-check",
                    tags: new string[] { "discountstorage" });
        }

        if (configuration.GetValue<bool>("AzureServiceBusEnabled")) {
            hcBuilder
                .AddAzureServiceBusTopic(
                    configuration["EventBusConnection"],
                    topicName: "eshop_event_bus",
                    name: "discount-servicebus-check",
                    tags: new string[] { "servicebus" });
        }
        else {
            hcBuilder
                .AddRabbitMQ(
                    $"amqp://{configuration["EventBusConnection"]}",
                    name: "discount-rabbitmqbus-check",
                    tags: new string[] { "rabbitmqbus" });
        }

        return services;
    }

    public static IServiceCollection AddCustomDbContext(this IServiceCollection services, IConfiguration configuration) {
        services.AddEntityFrameworkSqlServer()
            .AddDbContext<DiscountContext>(options => {
                options.UseSqlServer(configuration["ConnectionString"],
                                        sqlServerOptionsAction: sqlOptions => {
                                            sqlOptions.MigrationsAssembly(typeof(Startup).GetTypeInfo().Assembly.GetName().Name);
                                            //Configuring Connection Resiliency: https://docs.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency 
                                            sqlOptions.EnableRetryOnFailure(maxRetryCount: 15, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
                                        });
            });

        services.AddDbContext<IntegrationEventLogContext>(options => {
            options.UseSqlServer(configuration["ConnectionString"],
                                    sqlServerOptionsAction: sqlOptions => {
                                        sqlOptions.MigrationsAssembly(typeof(Startup).GetTypeInfo().Assembly.GetName().Name);
                                        //Configuring Connection Resiliency: https://docs.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency 
                                        sqlOptions.EnableRetryOnFailure(maxRetryCount: 15, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
                                    });
        });

        // Get the Catalog Settings service
        var thesis = configuration.GetValue<bool>("ThesisWrapperEnabled");

        if (thesis) {
            services.AddDbContext<GarbageContext>(options => {
                options.UseSqlServer(configuration["ConnectionString"],
                                                           sqlServerOptionsAction: sqlOptions => {
                                                               sqlOptions.MigrationsAssembly(typeof(Startup).GetTypeInfo().Assembly.GetName().Name);
                                                               //Configuring Connection Resiliency: https://docs.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency 
                                                               sqlOptions.EnableRetryOnFailure(maxRetryCount: 15, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
                                                           });
            });
        }

        return services;
    }

    // // Connection state change event handler
    // static void Connection_StateChange(object sender, StateChangeEventArgs e)
    // {
    //     SqlConnection connection = (SqlConnection)sender;
    //     Console.WriteLine($"Connection state changed: {e.OriginalState} => {e.CurrentState}");

    //     if (e.CurrentState == ConnectionState.Open)
    //     {
    //         Console.WriteLine($"Connection opened: {connection.DataSource}, Database: {connection.Database}");
    //     }
    //     else if (e.CurrentState == ConnectionState.Closed)
    //     {
    //         Console.WriteLine($"Connection closed: {connection.DataSource}, Database: {connection.Database}");
    //     }
    // }

    public static IServiceCollection AddCustomOptions(this IServiceCollection services, IConfiguration configuration) {
        services.Configure<DiscountSettings>(configuration);
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
                Title = "eShopOnContainers - Discount HTTP API",
                Version = "v1",
                Description = "The Discount Microservice HTTP API. This is a Data-Driven/CRUD microservice sample"
            });
        });

        return services;

    }

    public static IServiceCollection AddIntegrationServices(this IServiceCollection services, IConfiguration configuration) {
        Console.WriteLine($"Wrapper Enabled AddIntegrationServices: {configuration.GetValue<bool>("ThesisWrapperEnabled")}");

        services.AddTransient<Func<DbConnection, IIntegrationEventLogService>>(
            sp => (DbConnection c) => new IntegrationEventLogService(c));

        services.AddTransient<IDiscountIntegrationEventService, DiscountIntegrationEventService>();
        services.AddScoped<IFactoryClientIDWrappedProductDiscountChangedIntegrationEvent, FactoryClientIDWrappedProductDiscountChangedIntegrationEvent>();
        Console.WriteLine($"Wrapper Enabled: {configuration.GetValue<bool>("ThesisWrapperEnabled")}");
        if (configuration.GetValue<bool>("AzureServiceBusEnabled")) {
            Console.WriteLine("Azure Service Bus Enabled");
            services.AddSingleton<IServiceBusPersisterConnection>(sp => {
                var settings = sp.GetRequiredService<IOptions<DiscountSettings>>().Value;
                var serviceBusConnection = settings.EventBusConnection;

                return new DefaultServiceBusPersisterConnection(serviceBusConnection);
            });
        }
        else {
            Console.WriteLine("RabbitMQEnabled");
            try {
                services.AddSingleton<IRabbitMQPersistentConnection>(sp => {
                    var settings = sp.GetRequiredService<IOptions<DiscountSettings>>().Value;
                    var logger = sp.GetRequiredService<ILogger<DefaultRabbitMQPersistentConnection>>();
                    Console.WriteLine("Creating RabbitMQ Persistent Connection");
                    var factory = new ConnectionFactory() {
                        HostName = configuration["EventBusConnection"],
                        DispatchConsumersAsync = true
                    };
                    Console.WriteLine("Creating RabbitMQ Persistent Connection2");

                    if (!string.IsNullOrEmpty(configuration["EventBusUserName"])) {
                        factory.UserName = configuration["EventBusUserName"];
                    }
                    Console.WriteLine("Creating RabbitMQ Persistent Connection3");

                    if (!string.IsNullOrEmpty(configuration["EventBusPassword"])) {
                        factory.Password = configuration["EventBusPassword"];
                    }
                    Console.WriteLine("Creating RabbitMQ Persistent Connection4");

                    var retryCount = 5;
                    if (!string.IsNullOrEmpty(configuration["EventBusRetryCount"])) {
                        retryCount = int.Parse(configuration["EventBusRetryCount"]);
                    }

                    Console.WriteLine("Returning RabbitMQ Persistent Connection");
                    return new DefaultRabbitMQPersistentConnection(factory, logger, retryCount);
                });
            } catch (Exception e) {
                Console.WriteLine($"Exception: {e.Message}");
            }
            
        }

        return services;
    }

    public static IServiceCollection AddEventBus(this IServiceCollection services, IConfiguration configuration) {
        if (configuration.GetValue<bool>("AzureServiceBusEnabled")) {
            services.AddSingleton<IEventBus, EventBusServiceBus>(sp => {
                var serviceBusPersisterConnection = sp.GetRequiredService<IServiceBusPersisterConnection>();
                var iLifetimeScope = sp.GetRequiredService<ILifetimeScope>();
                var logger = sp.GetRequiredService<ILogger<EventBusServiceBus>>();
                var eventBusSubcriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();
                string subscriptionName = configuration["SubscriptionClientName"];

                return new EventBusServiceBus(serviceBusPersisterConnection, logger,
                    eventBusSubcriptionsManager, iLifetimeScope, subscriptionName);
            });

        }
        else {
            services.AddSingleton<IEventBus, EventBusRabbitMQ>(sp => {
                var subscriptionClientName = configuration["SubscriptionClientName"];
                var rabbitMQPersistentConnection = sp.GetRequiredService<IRabbitMQPersistentConnection>();
                var iLifetimeScope = sp.GetRequiredService<ILifetimeScope>();
                var logger = sp.GetRequiredService<ILogger<EventBusRabbitMQ>>();
                var eventBusSubcriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();

                var retryCount = 5;
                if (!string.IsNullOrEmpty(configuration["EventBusRetryCount"])) {
                    retryCount = int.Parse(configuration["EventBusRetryCount"]);
                }

                return new EventBusRabbitMQ(rabbitMQPersistentConnection, logger, iLifetimeScope, eventBusSubcriptionsManager, subscriptionClientName, retryCount);
            });
        }

        services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();

        return services;
    }
}
