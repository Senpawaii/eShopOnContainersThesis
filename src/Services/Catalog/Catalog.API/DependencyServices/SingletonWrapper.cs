using Google.Protobuf.WellKnownTypes;
using System.Collections.Concurrent;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Catalog.API.DependencyServices {
    public class SingletonWrapper : ISingletonWrapper {
        ConcurrentDictionary<string, ConcurrentBag<object[]>> wrapped_catalog_items = new ConcurrentDictionary<string, ConcurrentBag<object[]>>();
        ConcurrentDictionary<string, ConcurrentBag<object[]>> wrapped_catalog_types = new ConcurrentDictionary<string, ConcurrentBag<object[]>>();
        ConcurrentDictionary<string, ConcurrentBag<object[]>> wrapped_catalog_brands = new ConcurrentDictionary<string, ConcurrentBag<object[]>>();
        
        // Dictionary that hold the state of a transaction: PREPARE or COMMITTED
        ConcurrentDictionary<string, bool> transaction_state = new ConcurrentDictionary<string, bool>();

        public SingletonWrapper() {
        }

        public ConcurrentDictionary<string, ConcurrentBag<object[]>> SingletonWrappedCatalogItems {
            get { return wrapped_catalog_items; }
        }
        public ConcurrentDictionary<string, ConcurrentBag<object[]>> SingletonWrappedCatalogTypes {
            get { return wrapped_catalog_types; }
        }
        public ConcurrentDictionary<string, ConcurrentBag<object[]>> SingletonWrappedCatalogBrands {
            get { return wrapped_catalog_brands; }
        }

        public ConcurrentDictionary<string, bool> SingletonTransactionState {
            get { return transaction_state; }
        }

        public ConcurrentBag<object[]> SingletonGetCatalogITems(string key) {
            return wrapped_catalog_items.GetValueOrDefault(key);
        }
        public ConcurrentBag<object[]> SingletonGetCatalogTypes(string key) {
            return wrapped_catalog_types.GetValueOrDefault(key);
        }
        public ConcurrentBag<object[]> SingletonGetCatalogBrands(string key) {
            return wrapped_catalog_brands.GetValueOrDefault(key);
        }

        public bool SingletonGetTransactionState(string funcId) {
            return transaction_state.GetValueOrDefault(funcId);
        }

        public void SingletonAddCatalogItem(string funcID, IEnumerable<object[]> values) {
            foreach (object[] item in values) {
                wrapped_catalog_items.AddOrUpdate(funcID, new ConcurrentBag<object[]> { item }, (key, bag) => {
                    bag.Add(item);
                    return bag;
                });
            }
        }
        public void SingletonAddCatalogType(string funcID, IEnumerable<object[]> values) {
            foreach (object[] type in values) {
                wrapped_catalog_types.AddOrUpdate(funcID, new ConcurrentBag<object[]> { type }, (key, bag) => {
                    bag.Add(type);
                    return bag;
                });
            }
        }
        public void SingletonAddCatalogBrand(string funcID, IEnumerable<object[]> values) {
            foreach (object[] brand in values) {
                wrapped_catalog_brands.AddOrUpdate(funcID, new ConcurrentBag<object[]> { brand }, (key, bag) => {
                    bag.Add(brand);
                    return bag;
                });
            }
        }

        public bool SingletonSetTransactionState(string funcId, bool state) {
            return transaction_state[funcId] = state;
        }
    }
}
