using System.Collections.Concurrent;

namespace Catalog.API.DependencyServices {
    public interface ISingletonWrapper {
        ConcurrentDictionary<string, ConcurrentBag<CatalogItem>> SingletonWrappedCatalogItems { get; }
        ConcurrentDictionary<string, ConcurrentBag<CatalogType>> SingletonWrappedCatalogTypes { get; }
        ConcurrentDictionary<string, ConcurrentBag<CatalogBrand>> SingletonWrappedCatalogBrands { get; }
        ConcurrentDictionary<string, bool> SingletonTransactionState { get; }

        public ConcurrentBag<CatalogItem> SingletonGetCatalogITems(string key);
        public ConcurrentBag<CatalogType> SingletonGetCatalogTypes(string key);
        public ConcurrentBag<CatalogBrand> SingletonGetCatalogBrands(string key);
        public bool SingletonGetTransactionState(string funcId);

        public void SingletonAddCatalogItem(string funcID, CatalogItem item);
        public void SingletonAddCatalogType(string funcID, CatalogType type);
        public void SingletonAddCatalogBrand(string funcID, CatalogBrand brand);
        public bool SingletonSetTransactionState(string funcId, bool state);

        }
    }
