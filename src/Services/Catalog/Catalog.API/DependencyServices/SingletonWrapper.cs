using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Catalog.API.DependencyServices {
    public class SingletonWrapper : ISingletonWrapper {
        ConcurrentDictionary<string, ConcurrentBag<object[]>> wrapped_catalog_items = new ConcurrentDictionary<string, ConcurrentBag<object[]>>();
        ConcurrentDictionary<string, ConcurrentBag<object[]>> wrapped_catalog_types = new ConcurrentDictionary<string, ConcurrentBag<object[]>>();
        ConcurrentDictionary<string, ConcurrentBag<object[]>> wrapped_catalog_brands = new ConcurrentDictionary<string, ConcurrentBag<object[]>>();

        ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>> proposed_catalog_items = new ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>>();
        ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>> proposed_catalog_types = new ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>>();
        ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>> proposed_catalog_brands = new ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>>();

        ConcurrentDictionary<ProposedItem, ConcurrentDictionary<DateTime, int>> proposed_catalog_items2 = new ConcurrentDictionary<ProposedItem, ConcurrentDictionary<DateTime, int>>();
        ConcurrentDictionary<ProposedBrand, ConcurrentDictionary<DateTime, int>> proposed_catalog_brands2 = new ConcurrentDictionary<ProposedBrand, ConcurrentDictionary<DateTime, int>>();
        ConcurrentDictionary<ProposedType, ConcurrentDictionary<DateTime, int>> proposed_catalog_types2 = new ConcurrentDictionary<ProposedType, ConcurrentDictionary<DateTime, int>>();

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

        public ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>> Proposed_catalog_items {
            get { return proposed_catalog_items; } 
        }
        public ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>> Proposed_catalog_types {
            get { return proposed_catalog_types; }
        }
        public ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>> Proposed_catalog_brands {
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
                string identifiersStr = catalog_item_to_propose[1].ToString() + "_" + catalog_item_to_propose[2].ToString() + "_" + catalog_item_to_propose[4].ToString();

                proposed_catalog_items.AddOrUpdate(identifiersStr, _ => {
                    var innerDict = new ConcurrentDictionary<DateTime, int>();
                    innerDict.TryAdd(new DateTime(proposedTS), 1);
                    return innerDict;
                }, (_, value) => {
                    value.TryAdd(new DateTime(proposedTS), 1);
                    return value;
                });
            }

            foreach (object[] catalog_brand_to_propose in catalog_brands_to_propose) {
                // Brand: catalog_brand_to_propose[1]
                object[] identifiers = new object[] { catalog_brand_to_propose[1] };
                string identifiersStr = catalog_brand_to_propose[1].ToString();
                proposed_catalog_brands.AddOrUpdate(identifiersStr, _ => {
                    var innerDict = new ConcurrentDictionary<DateTime, int>();
                    innerDict.TryAdd(new DateTime(proposedTS), 1);
                    return innerDict;
                }, (_, value) => {
                    value.TryAdd(new DateTime(proposedTS), 1);
                    return value;
                });
            }

            foreach (object[] catalog_type_to_propose in catalog_types_to_propose) {
                // Type: catalog_type_to_propose[1]
                object[] identifiers = new object[] { catalog_type_to_propose[1] };
                string identifiersStr = catalog_type_to_propose[1].ToString();
                proposed_catalog_types.AddOrUpdate(identifiersStr, _ => {
                    var innerDict = new ConcurrentDictionary<DateTime, int>();
                    innerDict.TryAdd(new DateTime(proposedTS), 1);
                    return innerDict;
                }, (_, value) => {
                    value.TryAdd(new DateTime(proposedTS), 1);
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
                        string identifiersStr = object_to_remove[1].ToString() + "_" + object_to_remove[2].ToString() + "_" + object_to_remove[4].ToString();
                        var original_proposedTS = new DateTime(proposed_functionalities[clientID]);
                        proposed_catalog_items[identifiersStr].TryRemove(original_proposedTS, out _);
                    }
                    break;
                case "CatalogBrand":
                    foreach (object[] object_to_remove in wrapped_objects) {
                        // Brand: wrapped_object[1]
                        object[] identifiers = new object[] { object_to_remove[1] };
                        string identifiersStr = object_to_remove[1].ToString();
                        var original_proposedTS = new DateTime(proposed_functionalities[clientID]);
                        proposed_catalog_brands[identifiersStr].TryRemove(original_proposedTS, out _);
                    }
                    break;
                case "CatalogType":
                    foreach (object[] object_to_remove in wrapped_objects) {
                        // Type: wrapped_object[1]
                        object[] identifiers = new object[] { object_to_remove[1] };
                        string identifiersStr = object_to_remove[1].ToString();
                        var original_proposedTS = new DateTime(proposed_functionalities[clientID]);
                        proposed_catalog_types[identifiersStr].TryRemove(original_proposedTS, out _);
                    }
                    break;
            }
        }

        public bool AnyProposalWithLowerTimestamp(List<Tuple<string, string>> conditions, string targetTable, DateTime readerTimestamp) {
            switch (targetTable) {
                case "Catalog":
                    // Apply all the conditions to the set of proposed catalog items 2, this set will keep shrinking as we apply more conditions.
                    // Note that the original set of proposed catalog items 2 is not modified, we are just creating a new set with the results of the conditions.
                    var filtered_proposed_catalog_items2 = new Dictionary<ProposedItem, ConcurrentDictionary<DateTime, int>>(this.proposed_catalog_items2);
                    if (conditions != null) {
                        filtered_proposed_catalog_items2 = ApplyFiltersToCatalogItems(conditions, filtered_proposed_catalog_items2);
                    }
                    // Check if the there any proposed catalog items 2 with a lower timestamp than the reader's timestamp
                    foreach (KeyValuePair<ProposedItem, ConcurrentDictionary<DateTime, int>> proposed_catalog_item in filtered_proposed_catalog_items2) {
                        foreach (DateTime proposedTS in proposed_catalog_item.Value.Keys) {
                            if (proposedTS < readerTimestamp) {
                                // There is at least one proposed catalog item 2 with a lower timestamp than the reader's timestamp that might be committed before the reader's timestamp
                                return true;
                            }
                        }
                    }
                    break;
                case "CatalogBrand":
                    var filtered_proposed_catalog_brands2 = new Dictionary<ProposedBrand, ConcurrentDictionary<DateTime, int>>(this.proposed_catalog_brands2);
                    if (conditions != null) {
                        filtered_proposed_catalog_brands2 = ApplyFiltersToCatalogBrands(conditions, filtered_proposed_catalog_brands2);
                    }
                    // Check if the there any proposed catalog brands 2 with a lower timestamp than the reader's timestamp
                    foreach (KeyValuePair<ProposedBrand, ConcurrentDictionary<DateTime, int>> proposed_catalog_brand in filtered_proposed_catalog_brands2) {
                        foreach (DateTime proposedTS in proposed_catalog_brand.Value.Keys) {
                            if (proposedTS < readerTimestamp) {
                                // There is at least one proposed catalog brand 2 with a lower timestamp than the reader's timestamp that might be committed before the reader's timestamp
                                return true;
                            }
                        }
                    }

                    break;
                case "CatalogType":
                    var filtered_proposed_catalog_types2 = new Dictionary<ProposedType, ConcurrentDictionary<DateTime, int>>(this.proposed_catalog_types2);
                    if (conditions != null) {
                        filtered_proposed_catalog_types2 = ApplyFiltersToCatalogTypes(conditions, filtered_proposed_catalog_types2);
                    }
                    // Check if the there any proposed catalog types 2 with a lower timestamp than the reader's timestamp
                    foreach (KeyValuePair<ProposedType, ConcurrentDictionary<DateTime, int>> proposed_catalog_type in filtered_proposed_catalog_types2) {
                        foreach (DateTime proposedTS in proposed_catalog_type.Value.Keys) {
                            if (proposedTS < readerTimestamp) {
                                // There is at least one proposed catalog type 2 with a lower timestamp than the reader's timestamp that might be committed before the reader's timestamp
                                return true;
                            }
                        }
                    }
                    break;
            }

            return false;
        }

        private static Dictionary<ProposedType, ConcurrentDictionary<DateTime, int>> ApplyFiltersToCatalogTypes(List<Tuple<string, string>> conditions, Dictionary<ProposedType, ConcurrentDictionary<DateTime, int>> filtered_proposed_catalog_types2) {
            foreach (Tuple<string, string> condition in conditions) {
                filtered_proposed_catalog_types2 = filtered_proposed_catalog_types2.Where(x => x.Key.TypeName == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
            }

            return filtered_proposed_catalog_types2;
        }

        private static Dictionary<ProposedBrand, ConcurrentDictionary<DateTime, int>> ApplyFiltersToCatalogBrands(List<Tuple<string, string>> conditions, Dictionary<ProposedBrand, ConcurrentDictionary<DateTime, int>> filtered_proposed_catalog_brands2) {
            foreach (Tuple<string, string> condition in conditions) {
                filtered_proposed_catalog_brands2 = filtered_proposed_catalog_brands2.Where(x => x.Key.BrandName == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
            }

            return filtered_proposed_catalog_brands2;
        }

        private static Dictionary<ProposedItem, ConcurrentDictionary<DateTime, int>> ApplyFiltersToCatalogItems(List<Tuple<string, string>> conditions, Dictionary<ProposedItem, ConcurrentDictionary<DateTime, int>> filtered_proposed_catalog_items2) {
            foreach (Tuple<string, string> condition in conditions) {
                switch (condition.Item1) {
                    case "Name":
                        filtered_proposed_catalog_items2 = filtered_proposed_catalog_items2.Where(x => x.Key.Name == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
                        break;
                    case "BrandName":
                        filtered_proposed_catalog_items2 = filtered_proposed_catalog_items2.Where(x => x.Key.BrandName == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
                        break;
                    case "TypeName":
                        filtered_proposed_catalog_items2 = filtered_proposed_catalog_items2.Where(x => x.Key.TypeName == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
                        break;
                }
            }

            return filtered_proposed_catalog_items2;
        }
    }

    public struct ProposedItem {
        public string Name { get; set; }
        public string BrandName { get; set; }
        public string TypeName { get; set; }
    }

    public struct ProposedBrand {
        public string BrandName { get; set; }
    }

    public struct ProposedType {
        public string TypeName { get; set; }
    }
}
