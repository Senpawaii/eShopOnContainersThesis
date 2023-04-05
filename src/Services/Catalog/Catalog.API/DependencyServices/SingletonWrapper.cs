using Google.Protobuf.WellKnownTypes;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Catalog.API.DependencyServices {
    public class SingletonWrapper : ISingletonWrapper {
        ConcurrentDictionary<string, Tuple<int, int>> services_update_tracker = new ConcurrentDictionary<string, Tuple<int, int>>();

        public SingletonWrapper() {
        }

        public ConcurrentDictionary<string, Tuple<int, int>> SingletonWrapperServicesUpdateTracker {
            get { return services_update_tracker; }
        }

        public Tuple<int, int> SingletonWrapperServiceTracker(string key) {
            return services_update_tracker.GetValueOrDefault(key);
        }

        public void SingletonWrapperUpdateService(string key, Tuple<int, int> versions) {
            services_update_tracker.AddOrUpdate(key, versions, (key, oldValue) => versions);
        }
    }
}
