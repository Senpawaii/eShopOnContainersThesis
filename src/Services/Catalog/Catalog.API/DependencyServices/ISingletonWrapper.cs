using System.Collections.Concurrent;

namespace Catalog.API.DependencyServices {
    public interface ISingletonWrapper {
        ConcurrentDictionary<string, Tuple<int, int>> SingletonWrapperServicesUpdateTracker { get; }

        Tuple<int, int> SingletonWrapperServiceTracker(string key);

        void SingletonWrapperUpdateService(string key, Tuple<int, int> versions);

    }
}
