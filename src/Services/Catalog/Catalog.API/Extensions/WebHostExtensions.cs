using Autofac.Core;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Microsoft.eShopOnContainers.Services.Catalog.API.Extensions;

public static class WebHostExtensions
{
    public static bool IsInKubernetes(this IWebHost host)
    {
        var cfg = host.Services.GetService<IConfiguration>();
        var orchestratorType = cfg.GetValue<string>("OrchestratorType");
        return orchestratorType?.ToUpper() == "K8S";
    }

    public static IWebHost MigrateDbContext<TContext>(this IWebHost host, Action<TContext, IServiceProvider> seeder) where TContext : DbContext
    {
        var underK8s = host.IsInKubernetes();

        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;

        var logger = services.GetRequiredService<ILogger<TContext>>();

        var context = services.GetService<TContext>();

        try
        {
            logger.LogInformation("Migrating database associated with context {DbContextName}", typeof(TContext).Name);

            if (underK8s)
            {
                InvokeSeeder(seeder, context, services);
            }
            else
            {
                var retry = Policy.Handle<SqlException>()
                        .WaitAndRetry(new TimeSpan[]
                        {
                        TimeSpan.FromSeconds(3),
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(8),
                        });

                //if the sql server container is not created on run docker compose this
                //migration can't fail for network related exception. The retry options for DbContext only 
                //apply to transient exceptions
                // Note that this is NOT applied when running some orchestrators (let the orchestrator to recreate the failing service)
                retry.Execute(() => InvokeSeeder(seeder, context, services));
            }

            logger.LogInformation("Migrated database associated with context {DbContextName}", typeof(TContext).Name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while migrating the database used on context {DbContextName}", typeof(TContext).Name);
            if (underK8s)
            {
                throw;          // Rethrow under k8s because we rely on k8s to re-run the pod
            }
        }

        return host;
    }

    private static void InvokeSeeder<TContext>(Action<TContext, IServiceProvider> seeder, TContext context, IServiceProvider services)
        where TContext : DbContext
    {
        if(typeof(TContext) == typeof(CatalogContext)) {
            context.Database.GetService<IMigrator>().Migrate("20161103152832_Initial");
            context.Database.GetService<IMigrator>().Migrate("20161103153420_UpdateTableNames");
            context.Database.GetService<IMigrator>().Migrate("20170314083211_AddEventTable");
            context.Database.GetService<IMigrator>().Migrate("20170316012921_RefactoringToIntegrationEventLog");
            context.Database.GetService<IMigrator>().Migrate("20170316120022_RefactoringEventBusNamespaces");
            context.Database.GetService<IMigrator>().Migrate("20170322124244_RemoveIntegrationEventLogs");
            context.Database.GetService<IMigrator>().Migrate("20170509130025_AddStockProductItem");
            context.Database.GetService<IMigrator>().Migrate("20170530133114_AddPictureFile");

            var settings = services.GetService<IOptions<CatalogSettings>>();
            var thesisWrappers = settings.Value.ThesisWrapperEnabled;
            if (thesisWrappers) {
                context.Database.GetService<IMigrator>().Migrate("20230420112400_AddTimestampColumn");
            }
        } 
        else {
            context.Database.Migrate();
        }

        seeder(context, services);
    }
}
