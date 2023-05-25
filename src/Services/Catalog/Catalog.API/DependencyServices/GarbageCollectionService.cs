using System.Threading;

namespace Catalog.API.DependencyServices;

public class GarbageCollectionService : BackgroundService {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GarbageCollectionService> _logger;
    private int _executionCount = 0;
    private readonly int MAX_VERSIONS = 60;
    private readonly int TIMER = 8;

    public GarbageCollectionService(IServiceScopeFactory scopeFactory, ILogger<GarbageCollectionService> logger) {
        _scopeFactory = scopeFactory; 
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("Garbage Collection timed Hosted Service running.");

        // Execute an initial garbage collection
        ExecuteGarbageCollection();

        using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromSeconds(TIMER));

        try {
            while (await timer.WaitForNextTickAsync(stoppingToken)) {
                ExecuteGarbageCollection();
            }
        } catch (OperationCanceledException) {
            // Expected
            _logger.LogInformation("Garbage Collection timed Hosted Service is stopping.");
        }
    }

    private void ExecuteGarbageCollection() {
        using (var scope = _scopeFactory.CreateScope()) {
            var dbContext = scope.ServiceProvider.GetRequiredService<GarbageContext>();

            GarbageCollectCatalogItems(dbContext);
            GarbageCollectCatalogBrands(dbContext);
            GarbageCollectCatalogTypes(dbContext);
        }
    }

    private void GarbageCollectCatalogItems(GarbageContext dbContext) {
        var uniqueProducts = dbContext.CatalogItems.Select(p => new { p.Name, p.CatalogBrandId, p.CatalogTypeId }).Distinct().ToList();

        // For each unique product, fetch the number of versions
        foreach (var product in uniqueProducts) {
            var versions = dbContext.CatalogItems.Where(p => p.Name == product.Name && p.CatalogBrandId == product.CatalogBrandId && p.CatalogTypeId == product.CatalogTypeId).Count();
            if (versions > MAX_VERSIONS) {
                // Get all the product rows
                var productRows = dbContext.CatalogItems.Where(p => p.Name == product.Name && p.CatalogBrandId == product.CatalogBrandId && p.CatalogTypeId == product.CatalogTypeId).ToList();
                // Sort the product rows by timestamp
                productRows.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

                // Remove the oldest versions - MAX_VERSIONS versions, do not remove the first version (the seed version)
                var oldestVersions = productRows.GetRange(1, versions - MAX_VERSIONS);

                // Remove the oldest versions
                dbContext.CatalogItems.RemoveRange(oldestVersions);

                dbContext.SaveChanges();
            }
        }
    }

    private void GarbageCollectCatalogBrands(GarbageContext dbContext) {
        var uniqueBrands = dbContext.CatalogBrands.Select(p => new { p.Brand }).Distinct().ToList();
        // For each unique brand, fetch the number of versions
        foreach (var brand in uniqueBrands) {
            var versions = dbContext.CatalogBrands.Where(p => p.Brand == brand.Brand).Count();
            if (versions > MAX_VERSIONS) {
                // Get all the brand rows
                var brandRows = dbContext.CatalogBrands.Where(p => p.Brand == brand.Brand).ToList();
                // Sort the brand rows by timestamp
                brandRows.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                // Remove the oldest versions - MAX_VERSIONS versions do not remove the first version (the seed version)
                var oldestVersions = brandRows.GetRange(1, versions - MAX_VERSIONS);
                // Remove the oldest versions
                dbContext.CatalogBrands.RemoveRange(oldestVersions);
                dbContext.SaveChanges();
            }
        }
    }

    private void GarbageCollectCatalogTypes(GarbageContext dbContext) {
        var uniqueTypes = dbContext.CatalogTypes.Select(p => new { p.Type }).Distinct().ToList();
        // For each unique type, fetch the number of versions
        foreach (var type in uniqueTypes) {
            var versions = dbContext.CatalogTypes.Where(p => p.Type == type.Type).Count();
            if (versions > MAX_VERSIONS) {
                // Get all the type rows
                var typeRows = dbContext.CatalogTypes.Where(p => p.Type == type.Type).ToList();
                // Sort the type rows by timestamp
                typeRows.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                // Remove the oldest versions - MAX_VERSIONS versions do not remove the first version (the seed version)
                var oldestVersions = typeRows.GetRange(1, versions - MAX_VERSIONS);
                // Remove the oldest versions
                dbContext.CatalogTypes.RemoveRange(oldestVersions);
                dbContext.SaveChanges();
            }
        }
    }
}