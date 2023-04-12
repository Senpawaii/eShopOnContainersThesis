var configuration = GetConfiguration();

Log.Logger = CreateSerilogLogger(configuration);

try
{
    Log.Information("Configuring web host ({ApplicationContext})...", Program.AppName);
    var host = CreateHostBuilder(configuration, args);

    Log.Information("Applying migrations ({ApplicationContext})...", Program.AppName);
    host.MigrateDbContext<CatalogContext>((context, services) =>
    {
        var env = services.GetService<IWebHostEnvironment>();
        var settings = services.GetService<IOptions<CatalogSettings>>();
        var logger = services.GetService<ILogger<CatalogContextSeed>>();

        var thesisWrappers = settings.Value.ThesisWrapperEnabled;
        if (thesisWrappers) {
            updateTableColumns(configuration);
        }

        new CatalogContextSeed().SeedAsync(context, env, settings, logger).Wait();
    })
    .MigrateDbContext<IntegrationEventLogContext>((_, __) => { });

    Log.Information("Starting web host ({ApplicationContext})...", Program.AppName);
    host.Run();

    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Program terminated unexpectedly ({ApplicationContext})!", Program.AppName);
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

IWebHost CreateHostBuilder(IConfiguration configuration, string[] args) =>
  WebHost.CreateDefaultBuilder(args)
      .ConfigureAppConfiguration(x => x.AddConfiguration(configuration))
      .CaptureStartupErrors(false)
      .ConfigureKestrel(options =>
      {
          var ports = GetDefinedPorts(configuration);
          options.Listen(IPAddress.Any, ports.httpPort, listenOptions =>
          {
              listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
          });
          options.Listen(IPAddress.Any, ports.grpcPort, listenOptions =>
          {
              listenOptions.Protocols = HttpProtocols.Http2;
          });

      })
      .UseStartup<Startup>()
      .UseContentRoot(Directory.GetCurrentDirectory())
      .UseWebRoot("Pics")
      .UseSerilog()
      .Build();

Serilog.ILogger CreateSerilogLogger(IConfiguration configuration)
{
    var seqServerUrl = configuration["Serilog:SeqServerUrl"];
    var logstashUrl = configuration["Serilog:LogstashgUrl"];
    return new LoggerConfiguration()
        .MinimumLevel.Verbose()
        .Enrich.WithProperty("ApplicationContext", Program.AppName)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.Seq(string.IsNullOrWhiteSpace(seqServerUrl) ? "http://seq" : seqServerUrl)
        .WriteTo.Http(string.IsNullOrWhiteSpace(logstashUrl) ? "http://logstash:8080" : logstashUrl,null)
        .ReadFrom.Configuration(configuration)
        .CreateLogger();
}

(int httpPort, int grpcPort) GetDefinedPorts(IConfiguration config)
{
    var grpcPort = config.GetValue("GRPC_PORT", 81);
    var port = config.GetValue("PORT", 80);
    return (port, grpcPort);
}

IConfiguration GetConfiguration()
{
    var builder = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddEnvironmentVariables();

    var config = builder.Build();

    if (config.GetValue<bool>("UseVault", false))
    {
        TokenCredential credential = new ClientSecretCredential(
            config["Vault:TenantId"],
            config["Vault:ClientId"],
            config["Vault:ClientSecret"]);
        //builder.AddAzureKeyVault(new Uri($"https://{config["Vault:Name"]}.vault.azure.net/"), credential);        
    }

    return builder.Build();
}

void updateTableColumns(IConfiguration configuration) {
    // Add condition to not alter the table if the table was already altered -- This is only required if we don't clean the containers/ volumes between each iteration of the application.
    using (DbConnection connection = new SqlConnection(configuration["ConnectionString"])) {
        connection.Open();

        string[] tables = new string[3];
        tables[0] = "CatalogType";
        tables[1] = "CatalogBrand";
        tables[2] = "Catalog";

        for (int i = 0; i < 3; i++) {
            DbCommand command = new SqlCommand($"SELECT COUNT(*) FROM sys.columns WHERE Name = N'Timestamp' AND Object_ID = Object_ID(N'{tables[i]}')");
            command.Connection = connection;
            int result = (int)command.ExecuteScalar();
            if (result == 0) {
                AddColumnToTable(connection, tables[i]);
                Console.WriteLine($"Adding Timestamp column to Table {tables[i]}.");
            }
        }
    }
}

static void AddColumnToTable(DbConnection connection, string table) {
    DbCommand altercommand = new SqlCommand($"ALTER TABLE [Microsoft.eShopOnContainers.Services.CatalogDb].[dbo].[{table}] ADD [Timestamp] DATETIME NOT NULL DEFAULT GETDATE()");
    altercommand.Connection = connection;
    altercommand.ExecuteNonQuery();
}

public partial class Program
{
    public static string Namespace = typeof(Startup).Namespace;
    public static string AppName = Namespace.Substring(Namespace.LastIndexOf('.', Namespace.LastIndexOf('.') - 1) + 1);
}