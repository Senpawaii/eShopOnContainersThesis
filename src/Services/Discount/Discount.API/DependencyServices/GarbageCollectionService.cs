using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure;
using System.Threading;

namespace Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;

public class GarbageCollectionService : BackgroundService {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GarbageCollectionService> _logger;
    private readonly ISingletonWrapper _singletonWrapper;

    private int _executionCount = 0;
    private readonly int MAX_VERSIONS = 500;
    private readonly int TIMER = 4;

    public GarbageCollectionService(IServiceScopeFactory scopeFactory, ILogger<GarbageCollectionService> logger, ISingletonWrapper singletonWrapper) {
        _scopeFactory = scopeFactory; 
        _logger = logger;
        _singletonWrapper = singletonWrapper;
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

            GarbageCollectDiscountItems(dbContext);
        }
        _logger.LogInformation($"Garbage Collection timed Hosted Service is working. Count: {_executionCount++}");
        _singletonWrapper.DisposeCommittedDataMREs();
    }

    private void GarbageCollectDiscountItems(GarbageContext dbContext) {
        var uniqueDiscounts = dbContext.DiscountItems.Select(p => new { p.ItemName, p.ItemBrand, p.ItemType }).Distinct().ToList();

        // For each unique discount, fetch the number of versions
        foreach (var discount in uniqueDiscounts) {
            var versions = dbContext.DiscountItems.Where(p => p.ItemName == discount.ItemName && p.ItemBrand == discount.ItemBrand && p.ItemType == discount.ItemType).Count();
            if (versions > MAX_VERSIONS) {
                // Get all the Discount rows
                var discountRows = dbContext.DiscountItems.Where(p => p.ItemName == discount.ItemName && p.ItemBrand == discount.ItemBrand && p.ItemType == discount.ItemType).ToList();
                
                // Sort the discount rows by timestamp
                discountRows.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

                // Remove the oldest versions - MAX_VERSIONS versions, do not remove the first version (the seed version)
                var oldestVersions = discountRows.GetRange(1, versions - MAX_VERSIONS);

                // Remove the oldest versions
                dbContext.DiscountItems.RemoveRange(oldestVersions);

                dbContext.SaveChanges();
            }
        }
    }
}