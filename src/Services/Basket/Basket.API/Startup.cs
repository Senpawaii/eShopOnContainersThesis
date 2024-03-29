﻿using Microsoft.eShopOnContainers.Services.Basket.API.DependencyServices;
using Microsoft.eShopOnContainers.Services.Basket.API.Infrastructure.HttpHandlers;
using Microsoft.eShopOnContainers.Services.Basket.API.Middleware;
using Basket.API.DependencyServices;
using Basket.API.Infrastructure.RedisInterceptors;
using Microsoft.eShopOnContainers.Services.Basket.API.Infrastructure.Interceptors;

namespace Microsoft.eShopOnContainers.Services.Basket.API;
public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public virtual IServiceProvider ConfigureServices(IServiceCollection services)
    {
        services.AddGrpc(options =>
        {
            options.EnableDetailedErrors = true;
        });

        RegisterAppInsights(services);
        services.AddControllers(options =>
            {
                options.Filters.Add(typeof(HttpGlobalExceptionFilter));
                options.Filters.Add(typeof(ValidateModelStateFilter));

            }) // Added for functional tests
            .AddApplicationPart(typeof(BasketController).Assembly)
            .AddJsonOptions(options => options.JsonSerializerOptions.WriteIndented = true);

        services.AddSwaggerGen(options =>
        {            
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "eShopOnContainers - Basket HTTP API",
                Version = "v1",
                Description = "The Basket Service HTTP API"
            });

            options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows()
                {
                    Implicit = new OpenApiOAuthFlow()
                    {
                        AuthorizationUrl = new Uri($"{Configuration.GetValue<string>("IdentityUrlExternal")}/connect/authorize"),
                        TokenUrl = new Uri($"{Configuration.GetValue<string>("IdentityUrlExternal")}/connect/token"),
                        Scopes = new Dictionary<string, string>()
                        {
                            { "basket", "Basket API" }
                        }
                    }
                }
            });

            // Disabled for testing purposes
            //options.OperationFilter<AuthorizeCheckOperationFilter>();
        });

        // Disabled for testing purposes
        //ConfigureAuthService(services);

        services.AddCustomHealthCheck(Configuration);

        services.Configure<BasketSettings>(Configuration);

        //By connecting here we are making sure that our service
        //cannot start until redis is ready. This might slow down startup,
        //but given that there is a delay on resolving the ip address
        //and then creating the connection it seems reasonable to move
        //that cost to startup instead of having the first request pay the
        //penalty.
        services.AddSingleton<ConnectionMultiplexer>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<BasketSettings>>().Value;
            var configuration = ConfigurationOptions.Parse(settings.ConnectionString, true);

            return ConnectionMultiplexer.Connect(configuration);
        });


        if (Configuration.GetValue<bool>("AzureServiceBusEnabled"))
        {
            services.AddSingleton<IServiceBusPersisterConnection>(sp =>
            {
                var serviceBusConnectionString = Configuration["EventBusConnection"];

                return new DefaultServiceBusPersisterConnection(serviceBusConnectionString);
            });
        }
        else
        {
            services.AddSingleton<IRabbitMQPersistentConnection>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<DefaultRabbitMQPersistentConnection>>();

                var factory = new ConnectionFactory()
                {
                    HostName = Configuration["EventBusConnection"],
                    DispatchConsumersAsync = true
                };

                if (!string.IsNullOrEmpty(Configuration["EventBusUserName"]))
                {
                    factory.UserName = Configuration["EventBusUserName"];
                }

                if (!string.IsNullOrEmpty(Configuration["EventBusPassword"]))
                {
                    factory.Password = Configuration["EventBusPassword"];
                }

                var retryCount = 5;
                if (!string.IsNullOrEmpty(Configuration["EventBusRetryCount"]))
                {
                    retryCount = int.Parse(Configuration["EventBusRetryCount"]);
                }

                return new DefaultRabbitMQPersistentConnection(factory, logger, retryCount);
            });
        }

        RegisterEventBus(services);


        services.AddCors(options =>
        {
            options.AddPolicy("CorsPolicy",
                builder => builder
                .SetIsOriginAllowed((host) => true)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials());
        });
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddTransient<IBasketRepository, RedisBasketRepository>();
        services.AddTransient<IIdentityService, IdentityService>();

        services.AddTransient<IRedisDatabaseInterceptor, RedisDatabaseInterceptor>();
        services.AddScoped<IScopedMetadata, ScopedMetadata>();
        services.AddSingleton<ISingletonWrapper, SingletonWrapper>();

        // Register the HTTP client for Catalog.API and Discount.API
        if (Configuration["ThesisWrapperEnabled"] == "True") {
            // Register the HTTP Message Handler
            services.AddScoped<TCCHttpInjector>();
            
            services.AddSingleton<ITokensContextSingleton, TokensContextSingleton>();

            services.AddHttpClient<ICoordinatorService, CoordinatorService>();
            services.AddHttpClient<ICatalogService, CatalogService>().AddHttpMessageHandler<TCCHttpInjector>();
            services.AddHttpClient<IDiscountService, DiscountService>().AddHttpMessageHandler<TCCHttpInjector>();

        } else {
            services.AddHttpClient<ICatalogService, CatalogService>();
            services.AddHttpClient<IDiscountService, DiscountService>();
        }

        services.AddOptions();

        var container = new ContainerBuilder();
        container.Populate(services);

        return new AutofacServiceProvider(container.Build());
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
    {
        //loggerFactory.AddAzureWebAppDiagnostics();
        //loggerFactory.AddApplicationInsights(app.ApplicationServices, LogLevel.Trace);

        var pathBase = Configuration["PATH_BASE"];
        if (!string.IsNullOrEmpty(pathBase))
        {
            app.UsePathBase(pathBase);
        }

        app.UseSwagger()
            .UseSwaggerUI(setup =>
            {
                setup.SwaggerEndpoint($"{ (!string.IsNullOrEmpty(pathBase) ? pathBase : string.Empty) }/swagger/v1/swagger.json", "Basket.API V1");
                setup.OAuthClientId("basketswaggerui");
                setup.OAuthAppName("Basket Swagger UI");
            });

        app.UseRouting();
        app.UseCors("CorsPolicy");

        bool wrapperEnabled = Convert.ToBoolean(Configuration["ThesisWrapperEnabled"]);
        if (wrapperEnabled) {
            app.UseTCCMiddleware();
        }

        // Disabled for testing purposes
        //ConfigureAuth(app);

        app.UseStaticFiles();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGrpcService<BasketService>();
            endpoints.MapDefaultControllerRoute();
            endpoints.MapControllers();
            endpoints.MapGet("/_proto/", async ctx =>
            {
                ctx.Response.ContentType = "text/plain";
                using var fs = new FileStream(Path.Combine(env.ContentRootPath, "Proto", "basket.proto"), FileMode.Open, FileAccess.Read);
                using var sr = new StreamReader(fs);
                while (!sr.EndOfStream)
                {
                    var line = await sr.ReadLineAsync();
                    if (line != "/* >>" || line != "<< */")
                    {
                        await ctx.Response.WriteAsync(line);
                    }
                }
            });
            endpoints.MapHealthChecks("/hc", new HealthCheckOptions()
            {
                Predicate = _ => true,
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });
            endpoints.MapHealthChecks("/liveness", new HealthCheckOptions
            {
                Predicate = r => r.Name.Contains("self")
            });
        });

        ConfigureEventBus(app);
    }

    private void RegisterAppInsights(IServiceCollection services)
    {
        services.AddApplicationInsightsTelemetry(Configuration);
        services.AddApplicationInsightsKubernetesEnricher();
    }

    private void ConfigureAuthService(IServiceCollection services)
    {
        // prevent from mapping "sub" claim to nameidentifier.
        //JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Remove("sub");

        //var identityUrl = Configuration.GetValue<string>("IdentityUrl");

        //services.AddAuthentication("Bearer").AddJwtBearer(options =>
        //{
        //    options.Authority = identityUrl;
        //    options.RequireHttpsMetadata = false;
        //    options.Audience = "basket";
        //    options.TokenValidationParameters.ValidateAudience = false;
        //});
        //services.AddAuthorization(options =>
        //{
        //    options.AddPolicy("ApiScope", policy =>
        //    {
        //        policy.RequireAuthenticatedUser();
        //        policy.RequireClaim("scope", "basket");
        //    });
        //});
    }

    protected virtual void ConfigureAuth(IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

    private void RegisterEventBus(IServiceCollection services)
    {
        if (Configuration.GetValue<bool>("AzureServiceBusEnabled"))
        {
            services.AddSingleton<IEventBus, EventBusServiceBus>(sp =>
            {
                var serviceBusPersisterConnection = sp.GetRequiredService<IServiceBusPersisterConnection>();
                var iLifetimeScope = sp.GetRequiredService<ILifetimeScope>();
                var logger = sp.GetRequiredService<ILogger<EventBusServiceBus>>();
                var eventBusSubscriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();
                string subscriptionName = Configuration["SubscriptionClientName"];

                return new EventBusServiceBus(serviceBusPersisterConnection, logger,
                    eventBusSubscriptionsManager, iLifetimeScope, subscriptionName);
            });
        }
        else
        {
            services.AddSingleton<IEventBus, EventBusRabbitMQ>(sp =>
            {
                var subscriptionClientName = Configuration["SubscriptionClientName"];
                var rabbitMQPersistentConnection = sp.GetRequiredService<IRabbitMQPersistentConnection>();
                var iLifetimeScope = sp.GetRequiredService<ILifetimeScope>();
                var logger = sp.GetRequiredService<ILogger<EventBusRabbitMQ>>();
                var eventBusSubscriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();

                var retryCount = 5;
                if (!string.IsNullOrEmpty(Configuration["EventBusRetryCount"]))
                {
                    retryCount = int.Parse(Configuration["EventBusRetryCount"]);
                }

                return new EventBusRabbitMQ(rabbitMQPersistentConnection, logger, iLifetimeScope, eventBusSubscriptionsManager, subscriptionClientName, retryCount);
            });
        }

        services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();

        if (Configuration["ThesisWrapperEnabled"] == "True") {
            services.AddTransient<ClientIDWrappedProductPriceChangedIntegrationEventHandler>();
            services.AddTransient<ClientIDWrappedProductDiscountChangedIntegrationEventHandler>();
        }
        services.AddTransient<ProductPriceChangedIntegrationEventHandler>();
        services.AddTransient<ProductDiscountChangedIntegrationEventHandler>();
        services.AddTransient<OrderStartedIntegrationEventHandler>();
    }

    private void ConfigureEventBus(IApplicationBuilder app)
    {
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBus>();


        if (Configuration["ThesisWrapperEnabled"] == "True") {
            eventBus.Subscribe<ClientIDWrappedProductPriceChangedIntegrationEvent, ClientIDWrappedProductPriceChangedIntegrationEventHandler>();
            eventBus.Subscribe<ClientIDWrappedProductDiscountChangedIntegrationEvent, ClientIDWrappedProductDiscountChangedIntegrationEventHandler>();
            Console.WriteLine("\n\n\n=============== ThesisWrapperEnabled == True =============== \n\n\n");
        } else {
            eventBus.Subscribe<ProductPriceChangedIntegrationEvent, ProductPriceChangedIntegrationEventHandler>();
            eventBus.Subscribe<ProductDiscountChangedIntegrationEvent, ProductDiscountChangedIntegrationEventHandler>();
            Console.WriteLine("\n\n\n=============== ThesisWrapperEnabled == False =============== \n\n\n");
        }
        eventBus.Subscribe<OrderStartedIntegrationEvent, OrderStartedIntegrationEventHandler>();
    }
}
