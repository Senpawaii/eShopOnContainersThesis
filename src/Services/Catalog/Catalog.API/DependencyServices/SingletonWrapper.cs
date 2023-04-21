using Google.Protobuf.WellKnownTypes;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Catalog.API.DependencyServices {
    public class SingletonWrapper : ISingletonWrapper {
        ConcurrentDictionary<string, ConcurrentBag<CatalogItem>> wrapped_catalog_items = new ConcurrentDictionary<string, ConcurrentBag<CatalogItem>>();
        ConcurrentDictionary<string, ConcurrentBag<CatalogType>> wrapped_catalog_types = new ConcurrentDictionary<string, ConcurrentBag<CatalogType>>();
        ConcurrentDictionary<string, ConcurrentBag<CatalogBrand>> wrapped_catalog_brands = new ConcurrentDictionary<string, ConcurrentBag<CatalogBrand>>();
        
        // Dictionary that hold the state of a transaction: PREPARE or COMMITTED
        ConcurrentDictionary<string, bool> transaction_state = new ConcurrentDictionary<string, bool>();

        public SingletonWrapper() {
        }

        public ConcurrentDictionary<string, ConcurrentBag<CatalogItem>> SingletonWrappedCatalogItems {
            get { return wrapped_catalog_items; }
        }
        public ConcurrentDictionary<string, ConcurrentBag<CatalogType>> SingletonWrappedCatalogTypes {
            get { return wrapped_catalog_types; }
        }
        public ConcurrentDictionary<string, ConcurrentBag<CatalogBrand>> SingletonWrappedCatalogBrands {
            get { return wrapped_catalog_brands; }
        }

        public ConcurrentDictionary<string, bool> SingletonTransactionState {
            get { return transaction_state; }
        }

        public ConcurrentBag<CatalogItem> SingletonGetCatalogITems(string key) {
            return wrapped_catalog_items.GetValueOrDefault(key);
        }
        public ConcurrentBag<CatalogType> SingletonGetCatalogTypes(string key) {
            return wrapped_catalog_types.GetValueOrDefault(key);
        }
        public ConcurrentBag<CatalogBrand> SingletonGetCatalogBrands(string key) {
            return wrapped_catalog_brands.GetValueOrDefault(key);
        }

        public bool SingletonGetTransactionState(string funcId) {
            return transaction_state.GetValueOrDefault(funcId);
        }

        public void SingletonAddCatalogItem(string funcID, CatalogItem item) {
            wrapped_catalog_items.AddOrUpdate(funcID, new ConcurrentBag<CatalogItem> { item }, (key, bag) => {
                bag.Add(item);
                return bag;
            }); 
        }
        public void SingletonAddCatalogType(string funcID, CatalogType type) {
            wrapped_catalog_types.AddOrUpdate(funcID, new ConcurrentBag<CatalogType> { type }, (key, bag) => {
                bag.Add(type);
                return bag;
            });
        }
        public void SingletonAddCatalogBrand(string funcID, CatalogBrand brand) {
            wrapped_catalog_brands.AddOrUpdate(funcID, new ConcurrentBag<CatalogBrand> { brand }, (key, bag) => {
                bag.Add(brand);
                return bag;
            });
        }

        public bool SingletonSetTransactionState(string funcId, bool state) {
            return transaction_state[funcId] = state;
        }
    }
}
