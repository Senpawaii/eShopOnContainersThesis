using Discount.API;
using Microsoft.eShopOnContainers.BuildingBlocks.IntegrationEventLogEF;
using Microsoft.eShopOnContainers.Services.Discount.API;
using Microsoft.eShopOnContainers.Services.Discount.API.Extensions;
using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure;

var configuration = GetConfiguration();

Log.Logger = CreateSerilogLogger(configuration);

try {
    Log.Information($"Configuring web host ({Program.AppName})...");
    var host = CreateHostBuilder(configuration, args);

    Log.Information($"Applying migration ({Program.AppName})...");
    host.MigrateDbContext<DiscountContext>((context, services) => {
        var env = services.GetService<IWebHostEnvironment>();
        var settings = services.GetService<IOptions<DiscountSettings>>();
        var logger = services.GetService<ILogger<DiscountContextSeed>>();

        var thesisWrappers = settings.Value.ThesisWrapperEnabled;

        new DiscountContextSeed().SeedAsync(context, env, settings, logger).Wait();
    })
        .MigrateDbContext<IntegrationEventLogContext>((_, __) => { });

    Log.Information($"Starting web host ({Program.AppName})");
    host.Run();

    return 0;
} catch (Exception ex) {
    Log.Fatal(ex, $"Program terminated unexpectedly ({Program.AppName})");
    return 1;
} finally {
    Log.CloseAndFlush();
}

IWebHost CreateHostBuilder(IConfiguration configuration, string[] args) =>
  WebHost.CreateDefaultBuilder(args)
      .ConfigureAppConfiguration(x => x.AddConfiguration(configuration))
      .CaptureStartupErrors(false)
      .ConfigureKestrel(options => {
          var ports = GetDefinedPorts(configuration);
          options.Listen(IPAddress.Any, ports.httpPort, listenOptions => {
              listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
          });
          options.Listen(IPAddress.Any, ports.grpcPort, listenOptions => {
              listenOptions.Protocols = HttpProtocols.Http2;
          });

      })
      .UseStartup<Startup>()
      .UseContentRoot(Directory.GetCurrentDirectory())
      .UseWebRoot("Pics")
      .UseSerilog()
      .Build();

(int httpPort, int grpcPort) GetDefinedPorts(IConfiguration config) {
    var grpcPort = config.GetValue("GRPC_PORT", 81);
    var port = config.GetValue("PORT", 80);
    return (port, grpcPort);
}



Serilog.ILogger CreateSerilogLogger(IConfiguration configuration) {
    var seqServerUrl = configuration["Serilog:SeqServerUrl"];
    var logstashUrl = configuration["Serilog:LogstashUrl"];
    return new LoggerConfiguration()
        .MinimumLevel.Verbose()
        //.Enrich.WithProperty("ApplicationContext", Program.AppName)
        //.Enrich.FromLogContext()
        .WriteTo.Console()
        //.WriteTo.Seq(string.IsNullOrWhiteSpace(seqServerUrl) ? "http://seq" : seqServerUrl)
        //.WriteTo.Http(string.IsNullOrWhiteSpace(logstashUrl) ? "http://logstash:8080" : logstashUrl, null)
        .ReadFrom.Configuration(configuration)
        .CreateLogger();
}

IConfiguration GetConfiguration() {

    var builder = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddEnvironmentVariables();

    var config = builder.Build();

    if(config.GetValue<bool>("UseVault", false)) {
        TokenCredential credential = new ClientSecretCredential(
            config["Vault:TenantId"],
            config["Vault:ClientId"],
            config["Vault:ClientSecret"]);
    }

    return builder.Build();

}


public partial class Program {
    public static string Namespace = typeof(Startup).Namespace;
    public static string AppName = Namespace.Substring(Namespace.LastIndexOf('.', Namespace.LastIndexOf('.') - 1) + 1);
}
