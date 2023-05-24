namespace Catalog.API.DependencyServices;

public class GarbageCollectionWorker : IGarbageCollectionWorker {
    private readonly CatalogContext _dbContext;
    private readonly ILogger<GarbageCollectionWorker> _logger;

    public GarbageCollectionWorker(CatalogContext dbContext, ILogger<GarbageCollectionWorker> logger) {
        _dbContext = dbContext; 
        _logger = logger;
    }

    Task StartAsync(CancellationToken cancellationToken) {
        // Create a thread that runs every 15 seconds and checks if there are any products with more than 10 versions. If there are, remove the oldest version.
        while (true) {
            await Task.Delay(15000);
            // Perform garbage collection of the database context(s)

            // Fetch each uniq
        }


    }

    Task StopAsync(CancellationToken cancellationToken) {

    }

}