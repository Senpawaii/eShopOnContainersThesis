namespace Catalog.API.DependencyServices;

public interface IGarbageCollectionWorker {
    public void StartAsync();
    //public Task StopAsync();
}