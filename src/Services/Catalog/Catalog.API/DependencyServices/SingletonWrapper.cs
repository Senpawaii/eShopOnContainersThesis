using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Catalog.API.DependencyServices {
    public class SingletonWrapper : ISingletonWrapper {
        ConcurrentDictionary<string, ConcurrentBag<object[]>> wrapped_catalog_items = new ConcurrentDictionary<string, ConcurrentBag<object[]>>();
        ConcurrentDictionary<string, ConcurrentBag<object[]>> wrapped_catalog_types = new ConcurrentDictionary<string, ConcurrentBag<object[]>>();
        ConcurrentDictionary<string, ConcurrentBag<object[]>> wrapped_catalog_brands = new ConcurrentDictionary<string, ConcurrentBag<object[]>>();

        ConcurrentDictionary<string, ConcurrentBag<object[]>> proposed_catalog_items = new ConcurrentDictionary<string, ConcurrentBag<object[]>>();
        ConcurrentDictionary<string, ConcurrentBag<object[]>> proposed_catalog_types = new ConcurrentDictionary<string, ConcurrentBag<object[]>>();
        ConcurrentDictionary<string, ConcurrentBag<object[]>> proposed_catalog_brands = new ConcurrentDictionary<string, ConcurrentBag<object[]>>();

        // Store the proposed Timestamp for each functionality in Proposed State
        ConcurrentDictionary<string, long> proposed_functionalities = new ConcurrentDictionary<string, long>();

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

        public ConcurrentDictionary<string, ConcurrentBag<object[]>> Proposed_catalog_items {
            get { return proposed_catalog_items; } 
        }
        public ConcurrentDictionary<string, ConcurrentBag<object[]>> Proposed_catalog_types {
            get { return proposed_catalog_types; }
        }

        public ConcurrentDictionary<string, ConcurrentBag<object[]>> Proposed_catalog_brands {
            get { return proposed_catalog_brands; }
        }

        public ConcurrentDictionary<string, long> Proposed_functionalities {
            get { return proposed_functionalities; }
        }

        public ConcurrentBag<object[]> SingletonGetCatalogITems(string key) {
            return wrapped_catalog_items.GetValueOrDefault(key, new ConcurrentBag<object[]>());
        }
        public ConcurrentBag<object[]> SingletonGetCatalogTypes(string key) {
            return wrapped_catalog_types.GetValueOrDefault(key, new ConcurrentBag<object[]>());
        }
        public ConcurrentBag<object[]> SingletonGetCatalogBrands(string key) {
            return wrapped_catalog_brands.GetValueOrDefault(key, new ConcurrentBag<object[]>());
        }

        public bool SingletonGetTransactionState(string funcId) {
            if(!transaction_state.ContainsKey(funcId)) {
                // Initialize a new state: uncommitted
                transaction_state[funcId] = false;
            }
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

        // Remove the objects associated with the funcId
        public void SingletonRemoveFunctionalityObjects(string funcId) {
            if(wrapped_catalog_brands.ContainsKey(funcId))
                wrapped_catalog_brands.TryRemove(funcId, out ConcurrentBag<object[]> _);
            if(wrapped_catalog_types.ContainsKey(funcId))
                wrapped_catalog_types.TryRemove(funcId, out ConcurrentBag<object[]> _);
            if(wrapped_catalog_items.ContainsKey(funcId))
                wrapped_catalog_items.TryRemove(funcId, out ConcurrentBag<object[]> _);
            if(transaction_state.ContainsKey(funcId))
                transaction_state.TryRemove(funcId, out bool _);
        }

        public void SingletonAddProposedFunctionality(string functionality_ID, long proposedTS) {
            proposed_functionalities.AddOrUpdate(functionality_ID, proposedTS, (key, value) => {
                value = proposedTS;
                return value;
            });
        }

        public void SingletonRemoveProposedFunctionality(string functionality_ID) {
            if(proposed_functionalities.ContainsKey(functionality_ID))
                proposed_functionalities.TryRemove(functionality_ID, out long _);
        }

        public void SingletonAddWrappedItemsToProposedSet(string functionality_ID, long proposedTS) {
            // Gather the objects inside the wrapper with given functionality_ID
            ConcurrentBag<object[]> proposed_items = wrapped_catalog_items.GetValueOrDefault(functionality_ID, new ConcurrentBag<object[]>());
            ConcurrentBag<object[]> proposed_types = wrapped_catalog_types.GetValueOrDefault(functionality_ID, new ConcurrentBag<object[]>());
            ConcurrentBag<object[]> proposed_brands = wrapped_catalog_brands.GetValueOrDefault(functionality_ID, new ConcurrentBag<object[]>());

            // Add the objects to the list of proposed objects
            proposed_catalog_items.AddOrUpdate(functionality_ID, proposed_items, (key, bag) => {
                bag = proposed_items;
                return bag;
            });
            proposed_catalog_types.AddOrUpdate(functionality_ID, proposed_types, (key, bag) => {
                bag = proposed_types;
                return bag;
            });
            proposed_catalog_brands.AddOrUpdate(functionality_ID, proposed_brands, (key, bag) => {
                bag = proposed_brands;
                return bag;
            });
        }

        public void SingletonRemoveWrappedItemsFromProposedSet(string functionality_ID) {
            if(proposed_catalog_brands.ContainsKey(functionality_ID))
                proposed_catalog_brands.TryRemove(functionality_ID, out ConcurrentBag<object[]> _);
            if(proposed_catalog_types.ContainsKey(functionality_ID))
                proposed_catalog_types.TryRemove(functionality_ID, out ConcurrentBag<object[]> _);
            if(proposed_catalog_items.ContainsKey(functionality_ID))
                proposed_catalog_items.TryRemove(functionality_ID, out ConcurrentBag<object[]> _);
        }

    }
}
