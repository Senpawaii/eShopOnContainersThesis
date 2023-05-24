namespace Catalog.API.DependencyServices;

public interface IGarbageCollectionWorker {
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}