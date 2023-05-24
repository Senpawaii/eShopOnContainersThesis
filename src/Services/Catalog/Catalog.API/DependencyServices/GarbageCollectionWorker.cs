using System.Threading;

namespace Catalog.API.DependencyServices;

public class GarbageCollectionService : BackgroundService {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GarbageCollectionService> _logger;
    private int _executionCount = 0;
    private readonly int MAX_VERSIONS = 60;
    private readonly int TIMER = 8000;

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
            var uniqueProducts = dbContext.CatalogItems.Select(p => new { p.Name, p.CatalogBrandId, p.CatalogTypeId }).Distinct().ToList();

            // For each unique product, fetch the number of versions
            foreach (var product in uniqueProducts) {
                var versions = dbContext.CatalogItems.Where(p => p.Name == product.Name && p.CatalogBrandId == product.CatalogBrandId && p.CatalogTypeId == product.CatalogTypeId).Count();
                if (versions > MAX_VERSIONS) {
                    // Get all the product rows
                    var productRows = dbContext.CatalogItems.Where(p => p.Name == product.Name && p.CatalogBrandId == product.CatalogBrandId && p.CatalogTypeId == product.CatalogTypeId).ToList();
                    // Sort the product rows by timestamp
                    productRows.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

                    // Remove the oldest versions - MAX_VERSIONS versions
                    var oldestVersions = productRows.GetRange(0, versions - MAX_VERSIONS);

                    // Remove the oldest versions
                    dbContext.CatalogItems.RemoveRange(oldestVersions);

                    dbContext.SaveChanges();
                }
            }
        }
    }
}