﻿using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Catalog.API.DependencyServices {
    public class SingletonWrapper : ISingletonWrapper {
        ConcurrentDictionary<string, ConcurrentBag<object[]>> wrapped_catalog_items = new ConcurrentDictionary<string, ConcurrentBag<object[]>>();
        ConcurrentDictionary<string, ConcurrentBag<object[]>> wrapped_catalog_types = new ConcurrentDictionary<string, ConcurrentBag<object[]>>();
        ConcurrentDictionary<string, ConcurrentBag<object[]>> wrapped_catalog_brands = new ConcurrentDictionary<string, ConcurrentBag<object[]>>();

        ConcurrentDictionary<object[], ConcurrentDictionary<DateTime, int>> proposed_catalog_items = new ConcurrentDictionary<object[], ConcurrentDictionary<DateTime, int>>();
        ConcurrentDictionary<object[], ConcurrentDictionary<DateTime, int>> proposed_catalog_types = new ConcurrentDictionary<object[], ConcurrentDictionary<DateTime, int>>();
        ConcurrentDictionary<object[], ConcurrentDictionary<DateTime, int>> proposed_catalog_brands = new ConcurrentDictionary<object[], ConcurrentDictionary<DateTime, int>>();

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

        public ConcurrentDictionary<object[], ConcurrentDictionary<DateTime, int>> Proposed_catalog_items {
            get { return proposed_catalog_items; } 
        }
        public ConcurrentDictionary<object[], ConcurrentDictionary<DateTime, int>> Proposed_catalog_types {
            get { return proposed_catalog_types; }
        }
        public ConcurrentDictionary<object[], ConcurrentDictionary<DateTime, int>> Proposed_catalog_brands {
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

        public bool SingletonGetTransactionState(string clientID) {
            if(!transaction_state.ContainsKey(clientID)) {
                // Initialize a new state: uncommitted
                transaction_state[clientID] = false;
            }
            return transaction_state.GetValueOrDefault(clientID);
        }

        public void SingletonAddCatalogItem(string clientID, IEnumerable<object[]> values) {
            foreach (object[] item in values) {
                wrapped_catalog_items.AddOrUpdate(clientID, new ConcurrentBag<object[]> { item }, (key, bag) => {
                    bag.Add(item);
                    return bag;
                });
            }
        }
        public void SingletonAddCatalogType(string clientID, IEnumerable<object[]> values) {
            foreach (object[] type in values) {
                wrapped_catalog_types.AddOrUpdate(clientID, new ConcurrentBag<object[]> { type }, (key, bag) => {
                    bag.Add(type);
                    return bag;
                });
            }
        }
        public void SingletonAddCatalogBrand(string clientID, IEnumerable<object[]> values) {
            foreach (object[] brand in values) {
                wrapped_catalog_brands.AddOrUpdate(clientID, new ConcurrentBag<object[]> { brand }, (key, bag) => {
                    bag.Add(brand);
                    return bag;
                });
            }
        }

        public bool SingletonSetTransactionState(string clientID, bool state) {
            return transaction_state[clientID] = state;
        }

        // Remove the objects associated with the clientID
        public void SingletonRemoveFunctionalityObjects(string clientID) {
            if(wrapped_catalog_brands.ContainsKey(clientID))
                wrapped_catalog_brands.TryRemove(clientID, out ConcurrentBag<object[]> _);
            if(wrapped_catalog_types.ContainsKey(clientID))
                wrapped_catalog_types.TryRemove(clientID, out ConcurrentBag<object[]> _);
            if(wrapped_catalog_items.ContainsKey(clientID))
                wrapped_catalog_items.TryRemove(clientID, out ConcurrentBag<object[]> _);
            if(transaction_state.ContainsKey(clientID))
                transaction_state.TryRemove(clientID, out bool _);
        }

        public void SingletonAddProposedFunctionality(string clientID, long proposedTS) {
            proposed_functionalities.AddOrUpdate(clientID, proposedTS, (key, value) => {
                value = proposedTS;
                return value;
            });
        }

        public void SingletonRemoveProposedFunctionality(string clientID) {
            if(proposed_functionalities.ContainsKey(clientID))
                proposed_functionalities.TryRemove(clientID, out long _);
        }

        public void SingletonAddWrappedItemsToProposedSet(string clientID, long proposedTS) {
            // Gather the objects inside the wrapper with given clientID
            ConcurrentBag<object[]> catalog_items_to_propose = wrapped_catalog_items.GetValueOrDefault(clientID, new ConcurrentBag<object[]>());
            ConcurrentBag<object[]> catalog_types_to_propose = wrapped_catalog_types.GetValueOrDefault(clientID, new ConcurrentBag<object[]>());
            ConcurrentBag<object[]> catalog_brands_to_propose = wrapped_catalog_brands.GetValueOrDefault(clientID, new ConcurrentBag<object[]>());

            // For each object, add it to the proposed set using its identifiers, adding the proposed timestamp associated
            foreach (object[] catalog_item_to_propose in catalog_items_to_propose) {
                // Name: catalog_item_to_propose[1], BrandId: catalog_item_to_propose[2], TypeId: catalog_item_to_propose[4]
                object[] identifiers = new object[] { catalog_item_to_propose[1], catalog_item_to_propose[2], catalog_item_to_propose[4] };

                proposed_catalog_items[identifiers] = new ConcurrentDictionary<DateTime, int>();
                proposed_catalog_items[identifiers].AddOrUpdate(new DateTime(proposedTS), 1, (key, value) => {
                    value = 1;
                    return value;
                });
            }

            foreach (object[] catalog_brand_to_propose in catalog_brands_to_propose) {
                // Brand: catalog_brand_to_propose[1]
                object[] identifiers = new object[] { catalog_brand_to_propose[1] };
                proposed_catalog_brands[identifiers] = new ConcurrentDictionary<DateTime, int>();
                proposed_catalog_brands[identifiers].AddOrUpdate(new DateTime(proposedTS), 1, (key, value) => {
                    value = 1;
                    return value;
                });
            }

            foreach (object[] catalog_type_to_propose in catalog_types_to_propose) {
                // Type: catalog_type_to_propose[1]
                object[] identifiers = new object[] { catalog_type_to_propose[1] };
                proposed_catalog_types[identifiers] = new ConcurrentDictionary<DateTime, int>();
                proposed_catalog_types[identifiers].AddOrUpdate(new DateTime(proposedTS), 1, (key, value) => {
                    value = 1;
                    return value;
                });
            }
        }
         
        public void SingletonRemoveWrappedItemsFromProposedSet(string clientID, ConcurrentBag<object[]> wrapped_objects, string targetTable) {
            // Remove entries in the proposed set, identified by their table identifiers and the timestamp proposed in the functionality
            switch(targetTable) {
                case "Catalog":
                    foreach (object[] object_to_remove in wrapped_objects) {
                        // Name: wrapped_object[1], BrandId: wrapped_object[2], TypeId: wrapped_object[4]
                        object[] identifiers = new object[] { object_to_remove[1], object_to_remove[2], object_to_remove[4] };
                        var original_proposedTS = new DateTime(proposed_functionalities[clientID]);
                        foreach (object[] item in proposed_catalog_items.Keys) {
                            if (item[0].ToString() == identifiers[0].ToString() && item[1].ToString() == identifiers[1].ToString() && item[2].ToString() == identifiers[2].ToString()) {
                                proposed_catalog_items[item].TryRemove(original_proposedTS, out _);
                            }
                        }
                    }
                    break;
                case "CatalogBrand":
                    foreach (object[] object_to_remove in wrapped_objects) {
                        // Brand: wrapped_object[1]
                        object[] identifiers = new object[] { object_to_remove[1] };
                        var original_proposedTS = new DateTime(proposed_functionalities[clientID]);
                        foreach (object[] brand in proposed_catalog_brands.Keys) {
                            if (brand[0].ToString() == identifiers[0].ToString()) {
                                proposed_catalog_brands[brand].TryRemove(original_proposedTS, out _);
                            }
                        }
                    }
                    break;
                case "CatalogType":
                    foreach (object[] object_to_remove in wrapped_objects) {
                        // Type: wrapped_object[1]
                        object[] identifiers = new object[] { object_to_remove[1] };
                        var original_proposedTS = new DateTime(proposed_functionalities[clientID]);
                        foreach (object[] type in proposed_catalog_types.Keys) {
                            if (type[0].ToString() == identifiers[0].ToString()) {
                                proposed_catalog_types[type].TryRemove(original_proposedTS, out _);
                            }
                        }
                    }
                    break;
            }
        }

    }
}
