using System.Collections.Concurrent;

namespace Catalog.API.DependencyServices {
    public interface ISingletonWrapper {
        ConcurrentDictionary<string, ConcurrentBag<object[]>> SingletonWrappedCatalogItems { get; }
        ConcurrentDictionary<string, ConcurrentBag<object[]>> SingletonWrappedCatalogTypes { get; }
        ConcurrentDictionary<string, ConcurrentBag<object[]>> SingletonWrappedCatalogBrands { get; }
        ConcurrentDictionary<string, bool> SingletonTransactionState { get; }

        public ConcurrentBag<object[]> SingletonGetCatalogITems(string key);
        public ConcurrentBag<object[]> SingletonGetCatalogTypes(string key);
        public ConcurrentBag<object[]> SingletonGetCatalogBrands(string key);
        public bool SingletonGetTransactionState(string funcId);

        public void SingletonAddCatalogItem(string funcID, object[] item);
        public void SingletonAddCatalogType(string funcID, object[] type);
        public void SingletonAddCatalogBrand(string funcID, object[] brand);
        public bool SingletonSetTransactionState(string funcId, bool state);

        }
    }
